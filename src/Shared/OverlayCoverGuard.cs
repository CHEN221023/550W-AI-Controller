using System;
using System.Runtime.InteropServices;

namespace Controller550W.Rendering;

// Out-of-context foreground notifications only; no DLL or target-process hook.
// The callback lives on the owning UI thread and never activates our window.
internal sealed class OverlayCoverGuard : IDisposable
{
    delegate void EventProc(nint hook,uint evt,nint window,int obj,int child,uint thread,uint time);
    [DllImport("user32.dll")] static extern nint SetWinEventHook(uint first,uint last,nint module,EventProc callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] static extern bool UnhookWinEvent(nint hook);
    [DllImport("user32.dll")] static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    readonly nint window; readonly Func<bool> covering; readonly EventProc callback;
    GCHandle callbackRoot; nint hook; bool disposed;
    internal OverlayCoverGuard(nint window,Func<bool> covering)
    {
        this.window=window;this.covering=covering;
        callback=(_,_,_,_,_,_,_)=>Reassert();callbackRoot=GCHandle.Alloc(callback);
        hook=SetWinEventHook(3,3,0,callback,0,0,2); // FOREGROUND / OUTOFCONTEXT | SKIPOWNPROCESS
    }
    internal void Reassert()
    {
        if(!disposed&&covering())SetWindowPos(window,-1,0,0,0,0,0x13); // NOMOVE | NOSIZE | NOACTIVATE
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;if(hook!=0){UnhookWinEvent(hook);hook=0;}
        if(callbackRoot.IsAllocated)callbackRoot.Free();
    }
}
