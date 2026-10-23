using ProtoBuf;

namespace WolfConquer.Core.World;

[ProtoContract]
public class LegacyNativeWalkQuery
{
	[ProtoMember(1, IsRequired = true)]
	public uint Direction;

	[ProtoMember(2, IsRequired = true)]
	public uint UID;

	[ProtoMember(3, IsRequired = true)]
	public uint Running;

	[ProtoMember(4, IsRequired = true)]
	public uint TimeStamp;

	[ProtoMember(5)]
	public uint MapID;
}
