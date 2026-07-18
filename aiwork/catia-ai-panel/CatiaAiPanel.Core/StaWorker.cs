using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace CatiaAiPanel.Core;

internal sealed class StaWorker : IAsyncDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public StaWorker(string name)
    {
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (cancellationToken.IsCancellationRequested)
        {
            completion.SetCanceled(cancellationToken);
            return completion.Task;
        }

        try
        {
            _queue.Add(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                    return;
                }

                try { completion.TrySetResult(action()); }
                catch (Exception ex) { completion.TrySetException(ex); }
            });
        }
        catch (InvalidOperationException)
        {
            completion.TrySetException(new ObjectDisposedException(nameof(StaWorker)));
        }

        return completion.Task;
    }

    private void Run()
    {
        var initialized = NativeMethods.OleInitialize(IntPtr.Zero) >= 0;
        try
        {
            foreach (var action in _queue.GetConsumingEnumerable()) action();
        }
        finally
        {
            if (initialized) NativeMethods.OleUninitialize();
        }
    }

    public ValueTask DisposeAsync()
    {
        _queue.CompleteAdding();
        if (!_thread.Join(TimeSpan.FromSeconds(2))) { /* Never terminate an in-flight COM call. */ }
        _queue.Dispose();
        return ValueTask.CompletedTask;
    }

    private static class NativeMethods
    {
        [DllImport("ole32.dll")]
        internal static extern int OleInitialize(IntPtr reserved);

        [DllImport("ole32.dll")]
        internal static extern void OleUninitialize();
    }
}
