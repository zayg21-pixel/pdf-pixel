using System;
using System.Threading;
using System.Threading.Tasks;

public static class UiInvoker
{
    private static SynchronizationContext _uiContext;

    public static void Capture()
    {
        _uiContext = SynchronizationContext.Current
                     ?? throw new InvalidOperationException("No UI context found");
    }

    public static Task InvokeAsync(Action action)
    {
        if (_uiContext == null) throw new InvalidOperationException("UI context not captured");

        if (SynchronizationContext.Current == _uiContext)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();

        _uiContext.Post(_ =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }

    public static Task InvokeAsync(Func<Task> func)
    {
        if (_uiContext == null) throw new InvalidOperationException("UI context not captured");

        if (SynchronizationContext.Current == _uiContext)
        {
            return func();
        }

        var tcs = new TaskCompletionSource();

        _uiContext.Post(async _ =>
        {
            try
            {
                await func().ConfigureAwait(false);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, null);

        return tcs.Task;
    }
}
