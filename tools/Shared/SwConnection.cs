using System.Runtime.InteropServices;

// Shared by the tools that talk to a running SolidWorks from another process.

namespace SwTools;

internal static class ComHelpers
{
    /// <summary>Marshal.GetActiveObject doesn't exist in modern .NET; this is the same call.</summary>
    public static object? GetActiveObject(string progId)
    {
        if (CLSIDFromProgID(progId, out Guid clsid) != 0) return null;
        return GetActiveObject(ref clsid, IntPtr.Zero, out object? obj) == 0 ? obj : null;
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object? ppunk);
}

/// <summary>
/// When SolidWorks is busy (rebuilding, redrawing), Windows rejects calls from other programs.
/// This filter tells COM to retry for a while instead of throwing RPC_E_CALL_REJECTED.
/// </summary>
[ComVisible(true)]
internal sealed class MessageFilter : IOleMessageFilter
{
    public static void Register() => CoRegisterMessageFilter(new MessageFilter(), out _);

    int IOleMessageFilter.HandleInComingCall(int callType, IntPtr caller, int tickCount, IntPtr info) => 0;

    int IOleMessageFilter.RetryRejectedCall(IntPtr callee, int tickCount, int rejectType) =>
        rejectType == 2 && tickCount < 10_000 ? 50 : -1; // SERVERCALL_RETRYLATER: retry every 50 ms for up to 10 s

    int IOleMessageFilter.MessagePending(IntPtr callee, int tickCount, int pendingType) => 2; // PENDINGMSG_WAITDEFPROCESS

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);
}

[ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig] int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
    [PreserveSig] int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
    [PreserveSig] int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}
