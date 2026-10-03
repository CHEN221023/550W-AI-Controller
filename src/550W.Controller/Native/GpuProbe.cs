using System.Runtime.InteropServices;

namespace Controller550W.Controller.Native;

internal static class GpuProbe
{
    [DllImport("dxgi.dll")] static extern int CreateDXGIFactory1(ref Guid iid, out nint factory);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int EnumAdapters(nint self, uint index, out nint adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetDescription(nint self, out Description description);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Description
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Name;
        public uint Vendor, Device, Subsystem, Revision;
        public nuint DedicatedVideo, DedicatedSystem, Shared;
        public uint LuidLow; public int LuidHigh; public uint Flags;
    }
    static T Method<T>(nint obj, int index) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), index * nint.Size));
    public static (string Name, ulong DedicatedBytes)? Read()
    {
        var iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387"); nint factory = 0;
        try
        {
            if (CreateDXGIFactory1(ref iid, out factory) < 0) return null;
            var adapters = Method<EnumAdapters>(factory, 12); var choices = new List<Description>();
            for (uint index = 0; index < 16; index++)
            {
                if (adapters(factory, index, out var adapter) < 0) break;
                try { if (Method<GetDescription>(adapter, 10)(adapter, out var desc) == 0 && (desc.Flags & 2) == 0) choices.Add(desc); }
                finally { Marshal.Release(adapter); }
            }
            if (choices.Count == 0) return null;
            var best = choices.OrderByDescending(x => (ulong)x.DedicatedVideo).First();
            return (best.Name.Trim(), (ulong)best.DedicatedVideo);
        }
        catch { return null; }
        finally { if (factory != 0) Marshal.Release(factory); }
    }
}
