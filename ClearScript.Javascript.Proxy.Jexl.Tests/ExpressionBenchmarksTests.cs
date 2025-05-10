using Microsoft.ClearScript;
using Xunit;

namespace ClearScript.Javascript.Proxy.Jexl.Tests;

public class ExpressionBenchmarksTests : IDisposable
{
    private readonly IJexl _jexl;

    private readonly IScriptInstanceFactory<IJexl> _jexlScriptInstanceFactory;

    public ExpressionBenchmarksTests()
    {
        _jexlScriptInstanceFactory = ScriptProxyFactory.Create<JexlScriptInstanceFactory>();

        _jexl = _jexlScriptInstanceFactory.GetInstance()
                ?? throw new System.InvalidOperationException("Could not create IJexl");

        // Register transforms
        _jexl.AddTransform("upper", val => (string)val.ToString().ToUpperInvariant());
        _jexl.AddTransform("getSomeData", (val, args) =>
        {
            // simple arithmetic transform
            return (double)(val + Convert.ToInt32(args));
        });

        _jexl.AddTransform("getSomeDataAsync", async (val, args) =>
        {
            // simulate async work
            await Task.Yield();
            return $"async-{val}-{args}";
        });

        _jexlScriptInstanceFactory.GetNativeInstanceObject();
    }

    public void Dispose()
    {
        _jexlScriptInstanceFactory.Dispose();
    }

    // —————— Sync arithmetic ——————
    [Fact]
    public void SimpleArithmetic_ReturnsCorrect()
    {
        var result = _jexl.EvalSync<int>("a * b + c", new { a = 6, b = 7, c = 3 });
        Assert.Equal(6 * 7 + 3, result);
    }

    // —————— Property lookup / conditional ——————
    [Fact]
    public void PropertyLookup_BranchingWorks()
    {
        var expr = "user.age > 18 ? 'adult' : 'minor'";
        var result = _jexl.EvalSync<string>(expr, new { user = new { age = 17 } });
        Assert.Equal("minor", result);

        result = _jexl.EvalSync<string>(expr, new { user = new { age = 42 } });
        Assert.Equal("adult", result);
    }

    // —————— Sync transform no args ——————
    [Fact]
    public void Transform_SyncNoArgs_Works()
    {
        var result = _jexl.EvalSync<string>("value|upper", new { value = "hello" });
        Assert.Equal("HELLO", result);
    }

    // —————— Sync transform with args ——————
    [Fact]
    public void Transform_SyncWithArgs_Works()
    {
        var result = _jexl.EvalSync<int>("value|getSomeData(5)", new { value = 10 });
        Assert.Equal(10 + 5, result);
    }

    // —————— Async transform ——————
    [Fact]
    public async Task Transform_Async_Works()
    {
        var result = await _jexl.Eval<string>("value|getSomeDataAsync(3)", new { value = 7 });
        Assert.Equal("async-7-3", result);
    }

    // —————— Inline JS async function ——————
    [Fact]
    public async Task InlineAsyncFunction_Works()
    {
        const string fn = "value * value";
        var result = await _jexl.Eval<int>(fn, new { value = 8 });
        Assert.Equal(64, result);
    }

    // —————— Multiple transforms in pipeline ——————
    [Fact]
    public void MultipleTransforms_Pipeline_Works()
    {
        // register a second transform that doubles
        _jexl.AddTransform("double", v => 2 * (int)v);

        // pipeline: (value + 2) then double
        var expr = "value|getSomeData(2)|double";
        var result = _jexl.EvalSync<int>(expr, new { value = 4 });
        Assert.Equal((4 + 2) * 2, result);
    }

    // —————— Error in expression ——————
    [Fact]
    public void EvalSync_InvalidExpr_Throws()
    {
        var result = _jexl.EvalSync("nonexistent + 1", new { });


        Assert.Equal(double.NaN, result);
    }

    // —————— Promise rejection in transform ——————
    [Fact]
    public async Task Transform_SyncThrows_Propagates()
    {
        _jexl.AddTransform("boom", v =>
        {
            throw new InvalidOperationException("boom");

            return 1;
        });

        await Assert.ThrowsAsync<ScriptEngineException>(
            () => _jexl.Eval<object>("value|boom", new { value = 1 })
        );
    }

    // —————— Null and undefined contexts ——————
    [Fact]
    public void EvalSync_NullContext_Works()
    {
        // no variables used -> should still run
        var result = _jexl.EvalSync<int>("1 + 2", null!);
        Assert.Equal(3, result);
    }
}