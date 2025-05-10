namespace ClearScript.Javascript.Proxy;

[AttributeUsage(AttributeTargets.Method)]
public sealed class V8MethodNameAttribute(string name, bool isAsync = false) : Attribute
{
    public string Name { get; } = name;
    public bool IsAsync { get; } = isAsync;
}