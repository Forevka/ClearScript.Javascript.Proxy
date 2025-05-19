using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
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

    // —————— This should just run as it's native js ——————
    [Fact]
    public void Run_Bare_JavaScript()
    {
        var nativeJexl = _jexlScriptInstanceFactory.GetNativeInstanceObject();

        var nativeInstanceJexl = nativeJexl.GlobalName;

        var testCode = $"let jexl = {nativeInstanceJexl}" + Environment.NewLine;

        testCode = testCode + """
           debugger;
           const context = {
             name: { first: 'Sterling', last: 'Archer' },
             assoc: [
               { first: 'Lana', last: 'Kane' },
               { first: 'Cyril', last: 'Figgis' },
               { first: 'Pam', last: 'Poovey' }
             ],
             age: 36
           }

           // Filter an array asynchronously...
           const res = await jexl.eval('assoc[.first == "Lana"].last', context)
           console.log(res) // Output: Kane

           // Or synchronously!
           console.log(jexl.evalSync('assoc[.first == "Lana"].last', context)) // Output: Kane

           // Do math
           console.log(await jexl.eval('age * (3 - 1)', context))
           // 72

           // Concatenate
           console.log(await jexl.eval('name.first + " " + name["la" + "st"]', context))
           // "Sterling Archer"

           // Compound
           console.log(await jexl.eval(
             'assoc[.last == "Figgis"].first == "Cyril" && assoc[.last == "Poovey"].first == "Pam"',
             context
           ))
           // true

           // Use array indexes
           console.log(await jexl.eval('assoc[1]', context))
           // { first: 'Cyril', last: 'Figgis' }

           // Use conditional logic
           console.log(await jexl.eval('age > 62 ? "retired" : "working"', context))
           // "working"

           // Transform
           jexl.addTransform('upper', (val) => val.toUpperCase())
           console.log(await jexl.eval('"duchess"|upper + " " + name.last|upper', context))
           // "DUCHESS ARCHER"

           function dbSelectByLastName(val, stat) {
            return 184;
           }
           
           // Transform asynchronously, with arguments
           jexl.addTransform('getStat', async (val, stat) => dbSelectByLastName(val, stat))
           try {
             const res = await jexl.eval('name.last|getStat("weight")', context)
             console.log(res) // Output: 184
           } catch (e) {
             console.log('Database Error', e.stack)
           }

           // Functions too, sync or async, args or no args
           jexl.addFunction('getOldestAgent', () => {age: 1})
           console.log(await jexl.eval('age == getOldestAgent().age', context))
           // false

           // Add your own (a)synchronous operators
           // Here's a case-insensitive string equality
           jexl.addBinaryOp(
             '_=',
             20,
             (left, right) => left.toLowerCase() === right.toLowerCase()
           )
           console.log(await jexl.eval('"Guest" _= "gUeSt"'))
           // true

           // Compile your expression once, evaluate many times!
           const { expr } = jexl
           const danger = expr`"Danger " + place` // Also: jexl.compile('"Danger " + place')
           console.log(danger)
           console.log(danger.evalSync({ place: 'zone' })) // Danger zone
           console.log(danger.evalSync({ place: 'ZONE!!!' })) // Danger ZONE!!! (Doesn't recompile the expression!)
           
           """;

        nativeJexl.Runner.Execute(new DocumentInfo { Category = ModuleCategory.Standard }, testCode);

    }

}