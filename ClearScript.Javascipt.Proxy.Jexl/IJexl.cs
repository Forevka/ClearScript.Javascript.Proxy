namespace ClearScript.Javascript.Proxy.Jexl;

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
    void AddTransform(string expression, Func<dynamic, double> transformer);

    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, dynamic, string> transformer);
    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, dynamic, double> transformer);


    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, Task<string>> transformer);

    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, Task<double>> transformer);

    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, dynamic, Task<string>> transformer);
    [V8MethodName("addTransform")]
    void AddTransform(string expression, Func<dynamic, dynamic, Task<double>> transformer);


    // async; return a Task or Task<T>
    [V8MethodName("eval", isAsync: true)]
    Task<object> Eval(string expression, object context);

    [V8MethodName("eval", isAsync: true)]
    Task<T> Eval<T>(string expression, object context);
}

