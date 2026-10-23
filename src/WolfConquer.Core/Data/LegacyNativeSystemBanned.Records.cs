namespace WolfConquer.Core.Data;

// Native SystemBanned8..24: the original eagerly born dictionary and record.
internal partial class LegacyNativeSystemBanned
{
    public class Client
    {
        public string? IP = "";
        public uint Hours;
        public long StartBan;

        public override string ToString()
        {
            LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
            writeLine.Add(IP!).Add(Hours).Add(StartBan);
            return writeLine.Close();
        }
    }

    public static LegacyNativeDbSafeDictionary<string, Client> BannedPoll = new();
}
