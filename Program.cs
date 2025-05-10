using BenchmarkDotNet.Attributes;
using ClearScriptJint.Benchmark;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;

namespace JexlEngineBench
{
    public class Program
    {
        public static V8ScriptEngine _v8Engine;
        public static dynamic _v8Jexl;

        public static async Task Main(string[] args)
        {
            _v8Engine = new V8ScriptEngine(
                V8ScriptEngineFlags.EnableTaskPromiseConversion |
                V8ScriptEngineFlags.UseSynchronizationContexts);

            _v8Engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableAllLoading;
            _v8Engine.DocumentSettings.SearchPath = Path.GetFullPath("./jexl/lib");

            var code = File.ReadAllText(Path.Combine(_v8Engine.DocumentSettings.SearchPath, "Jexl.js"));
            _v8Engine.Evaluate(
                new DocumentInfo
                {
                    Category = ModuleCategory.CommonJS,
                },
                code
            );

            _v8Engine.Execute(new DocumentInfo { Category = ModuleCategory.CommonJS }, "globalThis.require = require");

            _v8Engine.Execute(
                "let jexlModule = require('Jexl.js');" +
                "globalThis.jexlInstance = new jexlModule.Jexl();"
            );

            var jexlInstance = _v8Engine.Script.jexlInstance;

            //var result = jexlInstance.evalSync("a + b", new { a = 2, b = 3 });  // 5

            //var jexl = _v8Engine.Create<IJexl>("jexlInstance");
            //int answer = jexl.EvalSync<int>("a + b", new {a = 2, b = 3});//new Dictionary<string, object> { { "a", 2 }, { "b", 3 } });

            var jexlFromFactory = _v8Engine.Create<JexlScriptInstanceFactory>() as IJexl;

            int answer2 = jexlFromFactory!.EvalSync<int>("a + b", new {a = 2, b = 3});


            var promise = await jexlFromFactory!.Eval<int>("value * 2", new { value = 9 });


            var a = await jexlFromFactory!.Eval<dynamic>("value * 2 * q", new { value = 9 });

            jexlFromFactory.AddTransform("upper", (a) =>
            {
                return "1";
            });

            var transformResult = jexlFromFactory!.EvalSync<dynamic>("value|upper", new { value = 9 });


            jexlFromFactory.AddTransform("getSomeData", (val, args) =>
            {
                return 1;
            });

            var transformResultWithArgs = jexlFromFactory!.EvalSync<dynamic>("value|getSomeData(1)", new { value = 9 });


            jexlFromFactory.AddTransform("getSomeDataAsync", async (val, args) =>
            {
                return "ASYNC";
            });

            var transformResultWithArgsAsync = await jexlFromFactory!.Eval<dynamic>("value|getSomeDataAsync(1)", new { value = 9 });

            //BenchmarkRunner.Run<ExpressionBenchmarks>();
            //promise.then(onResolved, onRejected); // writes "Resolved: 123"
            var b = 1;
        }
    }

    [MemoryDiagnoser]              // track allocations
    public class ExpressionBenchmarks
    {
        private V8ScriptEngine _v8Engine;
        private dynamic _v8Jexl;

        private Jint.Engine _jintEngine;
        private object _jintJexl;

        private IJexl jexl;
        
        private const string SimpleExpr = "a * b + c";
        private const string PropertyExpr = "user.age > 18 ? 'adult' : 'minor'";
        private const string FuncExpr = "async function check(x) { return x * x }; check(value)";

        private readonly object[] _varsSimple = { new { a = 6, b = 7, c = 3 } };
        private readonly object[] _varsProperty = { new { user = new { age = 42 } } };
        private readonly object[] _varsFunc = { new { value = 9 } };

        [GlobalSetup]
        public void Setup()
        {
            // --- ClearScript + V8 setup ---

            _v8Engine = new V8ScriptEngine();
            //_v8Engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableWebLoading;
            //var jexlCode = File.ReadAllText("./jexl/minified/jexl.min.js");

            //_v8Engine.DocumentSettings.SearchPath = @"D:\sources\repos\devcom\ClearScriptJint.Benchmark\jexl\";
            //_v8Engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;

            //_v8Engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableAllLoading;
            //_v8Engine.DocumentSettings.SearchPath = "https://cdn.jsdelivr.net/npm/jexl@2.3.0/dist/Jexl.min.js";

            _v8Engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableAllLoading;
            _v8Engine.DocumentSettings.SearchPath = Path.GetFullPath("./jexl/lib");

            var code = File.ReadAllText(Path.Combine(_v8Engine.DocumentSettings.SearchPath, "Jexl.js"));
            _v8Engine.Evaluate(
                new DocumentInfo
                {
                    Category = ModuleCategory.CommonJS,
                },
                code
            );

            _v8Engine.Execute(new DocumentInfo { Category = ModuleCategory.CommonJS }, "globalThis.require = require");

            _v8Engine.Execute(
                "let jexlModule = require('Jexl.js');" +
                "globalThis.jexlInstance = new jexlModule.Jexl();"
            );


            jexl = _v8Engine.Create<JexlScriptInstanceFactory>() as IJexl;

            //int answer2 = jexlFromFactory!.EvalSync<int>("a + b", new { a = 2, b = 3 });

            // --- Jint setup ---
            //_jintEngine = new Jint.Engine(cfg => cfg.LimitRecursion(100)
            //                                       .TimeoutInterval(TimeSpan.FromSeconds(5)));
            // load the same UMD bundle into Jint
            // var jexlBundle = File.ReadAllText("./jexl/minified/jexl.min.js");
            //_jintEngine.Execute(jexlBundle);
            //_jintEngine.Execute("var jexl = new Jexl.Jexl();");
            //_jintJexl = _jintEngine.GetValue("jexl").ToObject();
        }

        // ———————— Simple arithmetic ————————
        [Benchmark(Baseline = true)]
        public int ClearScript_Simple() =>
            jexl!.EvalSync<int>(SimpleExpr, _varsSimple[0]);

        //[Benchmark]
        public object Jint_Simple() =>
            _jintEngine.Invoke("jexl.evalSync", SimpleExpr, _varsSimple[0]).ToObject();

        // ———————— Property lookup ————————
        [Benchmark]
        public string ClearScript_Property() =>
            jexl!.EvalSync<string>(PropertyExpr, _varsProperty[0]);

        //[Benchmark]
        public object Jint_Property() =>
            _jintEngine.Invoke("jexl.evalSync", PropertyExpr, _varsProperty[0]).ToObject();

        // ———————— Async function call ————————
        [Benchmark]
        public object ClearScript_Async()
        {
            Action<object> onResolved = value => Console.WriteLine("Resolved: " + value);
            Action<object> onRejected = reason => Console.WriteLine("Rejected: " + reason);

            var a = jexl!.Eval<dynamic>(FuncExpr, _varsFunc[0]);
            //var promise = (dynamic)_v8Jexl.eval(FuncExpr, _varsFunc[0]);
            return 1; //promise.Result;
        }

        //[Benchmark]
        /*public async Task<object> Jint_Async()
        {
            // Jint’s async support varies—this just emulates awaiting a JS Promise
            var promise = _jintEngine.Invoke("jexl.eval", FuncExpr, _varsFunc[0]);
            // If Jexl returns a real Promise, you may need a helper to bridge it; adjust accordingly.
            return await Task.FromResult(promise.ToObject());
        }*/

        [GlobalCleanup]
        public void Cleanup()
        {
            _v8Engine.Dispose();
        }
    }
}
