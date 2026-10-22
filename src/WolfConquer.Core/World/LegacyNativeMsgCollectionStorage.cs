using ProtoBuf;

namespace WolfConquer.Core.World;

// Complete native message contract and leaf methods, not Process or a host route.
[ProtoContract]
internal partial class LegacyNativeMsgCollectionStorage
{
    [ProtoMember(1, IsRequired = true)]
    public Types Type = Types.STORAGE_LOAD;

    [ProtoMember(2, IsRequired = true)]
    public uint Identity = 0u;

    [ProtoMember(3, IsRequired = true)]
    public List<Scrap> pScrap = new List<Scrap>();

    [ProtoMember(4, IsRequired = true)]
    public List<item> items = new List<item>();

    [ProtoMember(5)]
    public uint UserID = 0u;
}
