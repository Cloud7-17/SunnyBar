using SunnyBar.App.Interop;

namespace SunnyBar.App.Services;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex mutex;
    private bool ownsMutex;

    public SingleInstanceGuard(string name = @"Local\SunnyBar.Desktop.v1")
    {
        mutex = new Mutex(false, name);
        try { ownsMutex = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { ownsMutex = true; }
    }

    public bool IsOwner => ownsMutex;

    public static void NotifyExistingInstance(bool openPreview)
    {
        var message = NativeMethods.RegisterWindowMessage(NativeMethods.ShowMessageName);
        if (message != 0) NativeMethods.PostMessage(NativeMethods.Broadcast, message, openPreview ? 1 : 0, 0);
    }

    public void Dispose()
    {
        if (ownsMutex) { mutex.ReleaseMutex(); ownsMutex = false; }
        mutex.Dispose();
    }
}
