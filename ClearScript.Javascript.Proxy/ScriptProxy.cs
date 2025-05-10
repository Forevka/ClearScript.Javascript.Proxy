namespace ClearScript.Javascript.Proxy;

using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using System.Reflection;

public class ScriptProxy<T> : DispatchProxy where T : IV8EngineObjectInstance
{
    public ScriptObject TargetObject { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            throw new InvalidOperationException("No method info");

        var attr = targetMethod.GetCustomAttribute<V8MethodNameAttribute>();
        var jsName = attr?.Name ?? targetMethod.Name;
        var isAsync = attr?.IsAsync ?? false;

        // raw is either a primitive, a HostObject, or (with our new flag) a Task/Task<T>
        var raw = TargetObject.InvokeMethod(jsName, args!);
        var returnType = targetMethod.ReturnType;

        // If this is a Task-returning method:
        if (typeof(Task).IsAssignableFrom(returnType) && raw is Task rawTask)
        {
            // Case A: non-generic Task
            if (returnType == typeof(Task))
            {
                return rawTask;
            }

            // Case B: generic Task<T>
            var genericDef = returnType.IsGenericType
                             && returnType.GetGenericTypeDefinition() == typeof(Task<>);

            if (genericDef)
            {
                // If V8 gave us Task<object>, we need to rewrap.
                var rawResultType = raw.GetType().GetGenericArguments()[0];
                var desiredResultType = returnType.GetGenericArguments()[0];

                if (rawResultType == typeof(object) && desiredResultType != typeof(object))
                {
                    // Invoke the generic wrapper WrapPromiseResult<T>
                    var wrapMethod = typeof(ScriptProxy<T>)
                        .GetMethod(nameof(WrapPromiseResult), BindingFlags.Instance | BindingFlags.NonPublic)!
                        .MakeGenericMethod(desiredResultType);

                    return wrapMethod.Invoke(this, new object[] { rawTask })!;
                }

                // If V8 actually returned Task<DesiredType>, just return it:
                return raw;
            }
        }

        // is 'void', we only wanted the side-effect:
        if (returnType == typeof(void))
        {
            return null;
        }

        if (returnType == typeof(object))
        {
            return raw;
        }

        return Convert.ChangeType(raw, targetMethod.ReturnType);
    }

    /// <summary>
    /// Takes a Task<object> (the Promise) and returns a Task<TResult>
    /// that awaits it, casts the boxed result into TResult, and
    /// propagates exceptions/cancellation.
    /// </summary>
    private async Task<TResult> WrapPromiseResult<TResult>(Task<object> promise)
    {
        object boxed = await promise.ConfigureAwait(false);

        return (TResult)Convert.ChangeType(boxed, typeof(TResult));
    }
}

public interface IScriptInstanceFactory<out TScriptTargetType> : IDisposable 
    where TScriptTargetType : class, IV8EngineObjectInstance
{
    public V8ScriptEngine Init();

    public void CreateNativeInstanceInEngine();

    public ScriptObject GetNativeInstanceObject();

    public TScriptTargetType GetInstance()
    {
        var proxy = DispatchProxy.Create<TScriptTargetType, ScriptProxy<TScriptTargetType>>() as ScriptProxy<TScriptTargetType>;
        proxy!.TargetObject = GetNativeInstanceObject();
        return proxy as TScriptTargetType ?? throw new InvalidCastException();
    }
}

public static class ScriptProxyFactory
{
    /*public static T Create<T>(this V8ScriptEngine engine, string globalVarName) where T : class, IV8EngineObjectInstance
    {
        var scriptObj = (ScriptObject)engine.Script[globalVarName];

        var proxy = DispatchProxy.Create<T, ScriptProxy<T>>() as ScriptProxy<T>;
        proxy!.TargetObject = scriptObj;
        return proxy as T ?? throw new InvalidCastException();
    }*/

    public static TScriptInstanceFactory Create<TScriptInstanceFactory>()
        where TScriptInstanceFactory : IScriptInstanceFactory<IV8EngineObjectInstance>, new()
    {
        var factory = new TScriptInstanceFactory();

        factory.Init();

        factory.CreateNativeInstanceInEngine();

        return factory;
    }
}

