internal static partial class Program
{
    private sealed class ConfigDecodeOperationException(Exception primary, Exception? cleanup) : Exception(
        "Decoder operation failed; primary and cleanup errors are retained.", primary)
    {
        internal Exception Primary { get; } = primary;
        internal Exception? Cleanup { get; } = cleanup;
    }

    private static void PublishConfigDecodeBytes(string output, byte[] bytes)
    {
        ValidateConfigDecodeOutput(output);
        var staged = Path.Combine(Path.GetDirectoryName(output)!, ".client-config-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        FileStream? lease = null;
        Exception? primary = null, cleanup = null;
        try
        {
            lease = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            lease.Write(bytes);
            lease.Flush(flushToDisk: true);
        }
        catch (Exception failure) { primary = failure; }
        finally { DisposeConfigDecodeLease(lease, ref cleanup); }
        try
        {
            if (primary is not null || cleanup is not null) ThrowConfigDecodeOperation(primary, cleanup);
            ValidateConfigDecodeOutput(output);
            File.Move(staged, output, overwrite: false);
        }
        catch (Exception failure)
        {
            primary = failure;
            cleanup = null;
            if (created)
            {
                try { File.Delete(staged); }
                catch (Exception error) { cleanup = error; }
            }
            ThrowConfigDecodeOperation(primary, cleanup);
        }
    }

    private static void ThrowConfigDecodeOperation(Exception? primary, Exception? cleanup)
    {
        if (primary is ConfigDecodeOperationException compound)
        {
            cleanup = compound.Cleanup is null ? cleanup : cleanup is null ? compound.Cleanup : new AggregateException(compound.Cleanup, cleanup);
            primary = compound.Primary;
        }
        throw new ConfigDecodeOperationException(primary ?? cleanup!, primary is null ? null : cleanup);
    }
}
