# ClearScript Javascript Proxy

This repository contains a set of .NET projects that demonstrate how to expose JavaScript libraries to C# using [ClearScript](https://microsoft.github.io/ClearScript/).  It includes a generic proxy system and an example integration with the [Jexl](https://github.com/TomFrost/Jexl) expression engine.

## Projects

- **ClearScript.Javascript.Proxy** – core library that builds typed proxies over JavaScript objects via `DispatchProxy`.
- **ClearScript.Javascipt.Proxy.Jexl** – wrapper around the Jexl library exposing a typed `IJexl` interface.
- **ClearScript.Javascript.Proxy.Jexl.Tests** – xUnit tests exercising the Jexl integration.
- **ClearScript.Javascript.Proxy.Jexl.Benchmark** – BenchmarkDotNet benchmarks for expression evaluation.
- **ClearScript.Javascipt.Proxy.Jexl.Playground** – small console application demonstrating Jexl usage.

## Building

All projects target **.NET 8.0**. Build the entire solution with:

```bash
 dotnet build ClearScriptJint.Benchmark.sln
```

The Jexl library is included as a local npm package under the `jexl` directory.  To produce the minified bundle used by the .NET projects run:

```bash
 npm install
 npm run build
```

## Running Tests

Execute the tests with the .NET CLI:

```bash
 dotnet test ClearScript.Javascript.Proxy.Jexl.Tests/ClearScript.Javascript.Proxy.Jexl.Tests.csproj
```

## Basic Usage

Create a Jexl instance from C# using the provided factory and call JavaScript functions as strongly typed methods:

```csharp
using ClearScript.Javascript.Proxy;
using ClearScript.Javascript.Proxy.Jexl;

// create and initialise the V8 engine with Jexl
IScriptInstanceFactory<IJexl> factory = ScriptProxyFactory.Create<JexlScriptInstanceFactory>();
IJexl jexl = factory.GetInstance();

int result = jexl.EvalSync<int>("a + b", new { a = 2, b = 3 });
Console.WriteLine(result); // 5
```

See the tests in `ClearScript.Javascript.Proxy.Jexl.Tests` for more examples.

