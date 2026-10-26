namespace WolfConquer.Core.Data;

internal sealed partial class WindowsJournalRootIdentity
{
    internal static WindowsJournalRootIdentity CapturePair(string first, string second)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows root identities are required.");
        var captures = new List<RootCapture>(2);
        try
        {
            captures.Add(CaptureRoot(first));
            captures.Add(CaptureRoot(second));
            return new(captures.ToArray());
        }
        catch (Exception error)
        {
            for (var index = captures.Count - 1; index >= 0; index--) captures[index].Dispose();
            if (error is DllNotFoundException or EntryPointNotFoundException)
                throw new PlatformNotSupportedException("Required native directory identity APIs are unavailable.", error);
            throw;
        }
    }
}
