using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;

namespace ClearScript.Javascript.Proxy.Jexl;

public class JexlScriptInstanceFactory : IScriptInstanceFactory<IJexl>
{
    private V8ScriptEngine _engine = null!;

    private readonly string _instanceId = Guid.NewGuid().ToString().Replace("-", "");
    private ScriptObject _instance = null!;

    private string _instanceGlobalName = null!;

    public V8ScriptEngine Init()
    {
        _instanceGlobalName = $"globalThis.jexlInstance{_instanceId}";

        _engine = new V8ScriptEngine(
            $"jexl-{_instanceId}",
            V8ScriptEngineFlags.EnableTaskPromiseConversion |
            V8ScriptEngineFlags.UseSynchronizationContexts);

        _engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableAllLoading;
        _engine.DocumentSettings.SearchPath = Path.GetFullPath("./jexl/lib");

        var code = File.ReadAllText(Path.Combine(_engine.DocumentSettings.SearchPath, "Jexl.js"));
        _engine.Evaluate(
            new DocumentInfo
            {
                Category = ModuleCategory.CommonJS,
            },
            code
        );

        _engine.Execute(new DocumentInfo { Category = ModuleCategory.CommonJS }, "globalThis.require = require");

        _engine.Execute(
            "let jexlModule = require('Jexl.js');" +
            "globalThis.jexlInstance = new jexlModule.Jexl();"
        );

        return _engine;
    }


    public void CreateNativeInstanceInEngine()
    {
        _engine.Execute(
            "if (typeof jexlModule === 'undefined') {" +
            "let jexlModule = require('Jexl.js');" +
            $"}}" +
            $"{_instanceGlobalName} = new jexlModule.Jexl();"
        );

        _instance = _engine.Script[$"{_instanceGlobalName}"];
    }

    public NativeInstanceObject GetNativeInstanceObject()
    {
        return new NativeInstanceObject
        {
            Instance = _instance,
            GlobalName = _instanceGlobalName,
        };
    }

    public void Dispose()
    {
        _engine.Dispose();
        _instance.Dispose();
    }
}

