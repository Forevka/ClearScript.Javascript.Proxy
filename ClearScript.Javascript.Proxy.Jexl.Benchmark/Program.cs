using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ClearScript.Javascript.Proxy;
using ClearScript.Javascript.Proxy.Jexl;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ExpressionBenchmarks>();
    }

    [MemoryDiagnoser]
    public class ExpressionBenchmarks
    {
        private IJexl _jexl;

        // Test expressions
        private const string SimpleExpr = "a * b + c";
        private const string PropertyExpr = "user.age > 18 ? 'adult' : 'minor'";
        private const string FuncExpr = "async function check(x) { return x * x; }; check(value)";
        private const string TransformExpr = "value|upper";
        private const string TransformArgs = "value|getSomeData(2)";
        private const string AsyncTransform = "value|getSomeDataAsync(3)";

        // Single var objects
        private readonly object _varsSimple = new { a = 6, b = 7, c = 3 };
        private readonly object _varsProperty = new { user = new { age = 42 } };
        private readonly object _varsFunc = new { value = 9 };
        private readonly object _varsTransform = new { value = "hello" };
        private readonly object _varsAsyncFunction = new { value = 9 };

        [GlobalSetup]
        public void Setup()
        {
            _jexl = ScriptProxyFactory.Create<JexlScriptInstanceFactory>() as IJexl
                    ?? throw new System.InvalidOperationException("Could not create IJexl");

            _jexl.AddTransform("upper", (val) =>
            {
                return (string)val.ToString().ToUpperInvariant();
            });
            _jexl.AddTransform("getSomeData", (val, args) =>
            {
                // example: return (int) args[0] + (int) val
                return ((object)args![0] is int iArg ? iArg : 0)
                     + ((object)val is int iVal ? iVal : 0);
            });
            _jexl.AddTransform("getSomeDataAsync", async (val, args) =>
            {
                // simulate async work
                await Task.Yield();
                return $"async-{val}-{args![0]}";
            });
        }

        // —————— Benchmarks ——————

        [Benchmark(Baseline = true)]
        public int ClearScript_Simple() =>
            _jexl.EvalSync<int>(SimpleExpr, _varsSimple);

        [Benchmark]
        public string ClearScript_Property() =>
            _jexl.EvalSync<string>(PropertyExpr, _varsProperty);

        [Benchmark]
        public string ClearScript_TransformSync() =>
            _jexl.EvalSync<string>(TransformExpr, _varsTransform);

        [Benchmark]
        public int ClearScript_TransformWithArgs() =>
            _jexl.EvalSync<int>(TransformArgs, _varsSimple /* reuse simple to pass an int */);

        [Benchmark]
        public async Task<string> ClearScript_TransformAsync() =>
            await _jexl.Eval<string>(AsyncTransform, _varsSimple);

        [Benchmark]
        public async Task<int> ClearScript_AsyncFunction() =>
            await _jexl.Eval<int>(FuncExpr, _varsFunc);

        [GlobalCleanup]
        public void Cleanup()
        {

        }
    }
}