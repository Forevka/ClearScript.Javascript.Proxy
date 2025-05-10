namespace ClearScript.Javascript.Proxy.Jexl.Playground;

internal class Program
{
    static void Main(string[] args)
    {
        IScriptInstanceFactory<IJexl> jexlScriptInstanceFactory = ScriptProxyFactory.Create<JexlScriptInstanceFactory>();

        var jexl = jexlScriptInstanceFactory.GetInstance()
                   ?? throw new System.InvalidOperationException("Could not create IJexl");

        var nativeJexl = jexlScriptInstanceFactory.GetNativeInstanceObject();


    }
}
