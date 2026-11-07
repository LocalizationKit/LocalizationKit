using Microsoft.Maui.Dispatching;

namespace LocalizationKit.Maui.Tests;

/// <summary>A dispatcher that runs what it is given, for a process with no UI thread.</summary>
/// <remarks>
/// <see cref="BindableObject"/>'s constructor asks for the current thread's dispatcher, and on
/// Windows that reaches WinUI's DispatcherQueue, which wants a registered WinAppSDK runtime that a
/// test process does not have. MAUI's own tests solve it the same way: supply a provider.
/// </remarks>
internal sealed class ImmediateDispatcher : IDispatcher
{
    public bool IsDispatchRequired => false;

    public bool Dispatch(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return true;
    }

    public IDispatcherTimer CreateTimer() => throw new NotSupportedException("No test here needs a timer.");
}

internal sealed class ImmediateDispatcherProvider : IDispatcherProvider
{
    public IDispatcher? GetForCurrentThread() => new ImmediateDispatcher();
}
