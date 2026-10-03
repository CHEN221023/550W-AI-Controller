namespace Controller550W.Core;

public enum ManagedClosePhase { Running, CloseRequested, ShutdownPreparing, WaitingConfirmation, ShutdownAnimating, AppClosing, Stopped }
public sealed class ManagedCloseLifecycle
{
    public ManagedClosePhase Phase { get; private set; } = ManagedClosePhase.Running;
    public string SessionId { get; private set; } = "";
    bool closeSent;
    public bool TryRequest()
    {
        if (Phase is not (ManagedClosePhase.Running or ManagedClosePhase.Stopped)) return false;
        SessionId = Guid.NewGuid().ToString("N"); closeSent = false; Phase = ManagedClosePhase.CloseRequested; return true;
    }
    public void Preparing() { if (Phase == ManagedClosePhase.CloseRequested) Phase = ManagedClosePhase.ShutdownPreparing; }
    public void WaitConfirmation() { if (Phase == ManagedClosePhase.ShutdownPreparing) Phase = ManagedClosePhase.WaitingConfirmation; }
    public bool Confirm() { if (Phase != ManagedClosePhase.WaitingConfirmation) return false; Phase = ManagedClosePhase.ShutdownAnimating; return true; }
    // The caller must verify its overlay has actually rendered before passing true.
    public bool RequestNormalClose(bool overlayVisible, bool failureFallback = false)
    {
        if (closeSent || Phase != ManagedClosePhase.ShutdownAnimating || (!overlayVisible && !failureFallback)) return false;
        closeSent = true; Phase = ManagedClosePhase.AppClosing; return true;
    }
    public void Cancel() { if (Phase is ManagedClosePhase.CloseRequested or ManagedClosePhase.ShutdownPreparing or ManagedClosePhase.WaitingConfirmation) Phase = ManagedClosePhase.Running; }
    public void Complete() => Phase = ManagedClosePhase.Stopped;
}
