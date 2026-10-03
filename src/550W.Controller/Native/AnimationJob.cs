using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Controller550W.Controller.Native;

/// <summary>Contains only our animation process and its WebView2 children.</summary>
internal sealed class AnimationJob : IDisposable
{
    nint handle;
    [StructLayout(LayoutKind.Sequential)] struct Basic { public long ProcessTime, JobTime; public uint Flags; public nuint Min, Max; public uint Active; public nuint Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] struct Io { public ulong ReadCount, WriteCount, OtherCount, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct Limits { public Basic Basic; public Io Io; public nuint ProcessMemory, JobMemory, PeakProcess, PeakJob; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern nint CreateJobObject(nint attributes, string? name);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(nint job, int type, ref Limits info, uint size);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(nint job, nint process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint handle);
    public AnimationJob(Process process)
    {
        handle = CreateJobObject(0, null); var limits = new Limits { Basic = new Basic { Flags = 0x2000 } };
        if (handle == 0 || !SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf<Limits>()) || !AssignProcessToJobObject(handle, process.Handle)) Dispose();
    }
    public void Dispose() { if (handle != 0) { CloseHandle(handle); handle = 0; } }
}
