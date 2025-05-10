namespace ClearScriptJint.Benchmark;

using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public sealed class V8MethodNameAttribute(string name, bool isAsync = false) : Attribute
{
    public string Name { get; } = name;
    public bool IsAsync { get; } = isAsync;
}

public interface IV8EngineObjectInstance;

public interface IJexl : IV8EngineObjectInstance
{
    // sync
    [V8MethodName("evalSync")]
    object EvalSync(string expression, object context);

    [V8MethodName("evalSync")]
    T EvalSync<T>(string expression, object context);

    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, string> transformer);

    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, decimal> transformer);

    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, dynamic, string> transformer);
    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, dynamic, decimal> transformer);


    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, Task<string>> transformer);

    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, Task<decimal>> transformer);

    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, dynamic, Task<string>> transformer);
    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, dynamic, Task<decimal>> transformer);


    // async; return a Task or Task<T>
    [V8MethodName("eval", isAsync: true)]
    Task<object> Eval(string expression, object context);

    [V8MethodName("eval", isAsync: true)]
    Task<T> Eval<T>(string expression, object context);


}

public class ScriptProxy<T> : DispatchProxy where T : IV8EngineObjectInstance
{
    internal ScriptObject TargetObject { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null)
            throw new InvalidOperationException("No method info");

        var attr = targetMethod.GetCustomAttribute<V8MethodNameAttribute>();
        var jsName = attr?.Name ?? targetMethod.Name;
        var isAsync = attr?.IsAsync ?? false;

        var raw = TargetObject.InvokeMethod(jsName, args!);

        // 3) if it's a Promise or marked async, wrap in a Task
        if (isAsync || raw is IJavaScriptObject {Kind: JavaScriptObjectKind.Promise})
        {
            var promise = (ScriptObject)raw;

            // Determine return signature
            var returnType = targetMethod.ReturnType;
            if (returnType == typeof(Task))
            {
                var tcs = new TaskCompletionSource<object?>();
                // then(onFulfilled, onRejected)
                promise.InvokeMethod("then",
                    new Action<object?>(val => tcs.SetResult(null)),
                    new Action<object?>(err => tcs.SetException(
                        new ScriptEngineException(err?.ToString()))));
                return tcs.Task;
            }

            if (returnType.IsGenericType &&
                returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];    // e.g. typeof(int)
                var tcsType = typeof(TaskCompletionSource<>)
                    .MakeGenericType(resultType);
                var tcs = Activator.CreateInstance(tcsType)!;

                // cache
                var setResult = tcsType.GetMethod("SetResult", [resultType])!;
                var setException = tcsType.GetMethod("SetException", [typeof(Exception)])!;

                /*
                promise.InvokeMethod("catch",
                    new Action<object?>(value =>
                    {

                        Console.WriteLine("Exception: " + value);
                    })
                );*/

                promise.InvokeMethod("then",
                    new Action<object?>(value =>
                    {
                        var converted = Convert.ChangeType(value, resultType);
                        setResult.Invoke(tcs, [converted]);
                    }),
                    new Action<object?>(reason =>
                    {
                        setException.Invoke(tcs, [
                            new ScriptEngineException(reason?.ToString())
                        ]);
                    })
                );

                // return the Task<T>
                return tcsType
                    .GetProperty("Task")!
                    .GetValue(tcs)!;
            }

            throw new InvalidOperationException($"Async JS method mapped to non‐Task return: {targetMethod.Name}");
        }

        // 4) synchronous fall-back
        var ret = raw;
        var rt = targetMethod.ReturnType;
        if (rt == typeof(void) || rt == typeof(object))
            return ret;

        return Convert.ChangeType(ret, rt);
    }
}

public interface IScriptInstanceFactory<out TScriptTargetType> where TScriptTargetType : IV8EngineObjectInstance
{
    public void AssignEngine(V8ScriptEngine engine);
    public void CreateInstanceInEngine();

    public TScriptTargetType GetInstance();
}

public class JexlScriptInstanceFactory : IScriptInstanceFactory<IJexl>
{
    private V8ScriptEngine _engine = null!;

    private readonly string _instanceId = Guid.NewGuid().ToString().Replace("-", "");
    private ScriptObject _instance = null!;

    public void AssignEngine(V8ScriptEngine engine)
    {
        _engine = engine;
    }

    public void CreateInstanceInEngine()
    {
        _engine.Execute(
            "if (typeof jexlModule === 'undefined') {" +
            "let jexlModule = require('Jexl.js');" +
            $"}}" +
            $"globalThis.jexlInstance{_instanceId} = new jexlModule.Jexl();"
        );

        _instance = _engine.Script[$"jexlInstance{_instanceId}"];
    }

    public IJexl GetInstance()
    {
        var proxy = DispatchProxy.Create<IJexl, ScriptProxy<IJexl>>() as ScriptProxy<IJexl>;
        proxy!.TargetObject = _instance;
        return proxy as IJexl ?? throw new InvalidCastException();
    }
}


public static class ScriptProxyFactory
{
    public static T Create<T>(this V8ScriptEngine engine, string globalVarName) where T : class, IV8EngineObjectInstance
    {
        var scriptObj = (ScriptObject)engine.Script[globalVarName];

        var proxy = DispatchProxy.Create<T, ScriptProxy<T>>() as ScriptProxy<T>;
        proxy!.TargetObject = scriptObj;
        return proxy as T ?? throw new InvalidCastException();
    }

    public static IV8EngineObjectInstance Create<TScriptInstanceFactory>(this V8ScriptEngine engine) where TScriptInstanceFactory : IScriptInstanceFactory<IV8EngineObjectInstance>, new()
    {
        var factory = new TScriptInstanceFactory();
        factory.AssignEngine(engine);

        factory.CreateInstanceInEngine();

        return factory.GetInstance();
    }
}

