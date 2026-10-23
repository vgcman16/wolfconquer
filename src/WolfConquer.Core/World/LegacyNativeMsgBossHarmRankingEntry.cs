using ProtoBuf;

namespace WolfConquer.Core.World;

[ProtoContract]
internal class LegacyNativeMsgBossHarmRankingEntry
{
	[ProtoMember(1, IsRequired = true)]
	public uint Rank;

	[ProtoMember(2, IsRequired = true)]
	public uint ServerID;

	[ProtoMember(3, IsRequired = true)]
	public uint HunterUID;

	[ProtoMember(4, IsRequired = true)]
	public string? HunterName;

	[ProtoMember(5, IsRequired = true)]
	public uint HunterScore;
}
