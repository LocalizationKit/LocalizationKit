using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace LocalizationKit.Wpf.Tests;

/// <summary>Runs a test the way an application runs: on an STA thread with a dispatcher.</summary>
/// <remarks>
/// <para>
/// WPF refuses to build an element on a thread that is not STA, and xUnit's threads are not. The
/// usual answer is a package that supplies an <c>[STAFact]</c> attribute; this is a dozen lines
/// instead, and it buys something that attribute would not: a real <see cref="Dispatcher"/>.
/// </para>
/// <para>
/// That matters here because the culture can be switched from any thread, so
/// <c>LocalizationSource</c> marshals its change notification to the dispatcher that owns the
/// elements. Without one, the notification would arrive on whichever thread the await resumed on and
/// the test would pass for the wrong reason — or fail for a reason no application would hit.
/// </para>
/// </remarks>
internal static class Sta
{
    public static void Run(Func<Task> work)
    {
        ExceptionDispatchInfo? failure = null;

        Thread thread = new(() =>
        {
            try
            {
                Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

                DispatcherFrame frame = new();

                _ = dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        await work();
                    }
                    catch (Exception exception)
                    {
                        failure = ExceptionDispatchInfo.Capture(exception);
                    }
                    finally
                    {
                        // Stop pumping, which lets the thread end and the test finish.
                        frame.Continue = false;
                    }
                });

                Dispatcher.PushFrame(frame);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The test did not finish on its dispatcher thread.");
        }

        failure?.Throw();
    }
}
