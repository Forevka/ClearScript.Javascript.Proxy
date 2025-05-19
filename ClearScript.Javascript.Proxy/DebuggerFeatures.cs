using Microsoft.ClearScript.V8;

namespace ClearScript.Javascript.Proxy;

public class DebuggerFeatures
{
    private bool _isEnabled = false;

    public void Enable()
    {
        if (_isEnabled) return;
        V8Runtime.DebuggerConnected += OnDebuggerConnected;
        V8Runtime.DebuggerDisconnected += OnDebuggerDisconnected;

        _isEnabled = true;
    }

    void OnDebuggerConnected(object? sender, V8RuntimeDebuggerEventArgs args)
    {
        // args.V8Runtime is the runtime that got attached
        // args.Port is the TCP port the debugger used
        Console.WriteLine(
            $"[DebuggerConnected] runtime='{args.Name}' port={args.Port}"
        );
    }

    void OnDebuggerDisconnected(object? sender, V8RuntimeDebuggerEventArgs args)
    {
        Console.WriteLine(
            $"[DebuggerDisconnected] runtime='{args.Name}'"
        );
    }
}
