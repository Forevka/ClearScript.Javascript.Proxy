using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript;

namespace ClearScript.Javascript.Proxy.Jexl.Playground;

internal class Program
{
    static void Main(string[] args)
    {
        IScriptInstanceFactory<IJexl> jexlScriptInstanceFactory = ScriptProxyFactory.Create<JexlScriptInstanceFactory>();

        var nativeJexl = jexlScriptInstanceFactory.GetNativeInstanceObject();

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


        var global = (ScriptObject)nativeJexl.Runner.Global;

        foreach (var name in global.PropertyNames)
        {
            Console.WriteLine(name);
        }


    }
}
