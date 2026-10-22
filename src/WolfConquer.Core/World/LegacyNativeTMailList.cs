namespace WolfConquer.Core.World;

// Complete native TMailList physical record. Native default sequential layout is retained.
internal partial struct LegacyNativeTMailList
{
    public unsafe fixed sbyte _title[64];
    public unsafe fixed sbyte _Sender_name[128];
    public unsafe fixed sbyte _content[512];
    public uint id;
    public ulong Money;
    public uint EMoney;
    public uint itemid;
    public uint Emoney_Record_Type;
    public uint Flag;
    public long Timer;
}
