namespace WolfConquer.Core.Data;

internal partial class LegacyNativeSystemBannedAccount
{
    public class Client
    {
        public uint UID;
        public string? Name;
        public uint Hours;
        public long StartBan;
        public string? Reason = "";

        public override string ToString()
        {
            LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
            writeLine.Add(UID).Add(Hours).Add(StartBan).Add(Name!).Add(Reason!);
            return writeLine.Close();
        }
    }

    public static LegacyNativeDbSafeDictionary<uint, Client> BannedPoll = new();
}
