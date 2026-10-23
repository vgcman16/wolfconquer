using System;
using ProtoBuf;

namespace WolfConquer.Core.World;

// Entire native attributed contract; entry is the existing real record owner.
[ProtoContract]
internal struct LegacyNativeMsgBossHarmRanking
{
    [Flags]
    public enum RankAction
    {
        Show = 0,
        ShowRespondForFirstThree = 1,
        ShowRespondForTheRest = 2
    }

    [ProtoMember(1, IsRequired = true)]
    public RankAction Type;

    [ProtoMember(2, IsRequired = true)]
    public uint MonsterID;

    [ProtoMember(3, IsRequired = true)]
    public LegacyNativeMsgBossHarmRankingEntry[]? Hunters;
}
