#nullable disable
using ProtoBuf;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Complete native physical contracts. The native Process receiver remains absent.
internal static partial class LegacyNativeMsgHundredWeaponsOpt
{
    [ProtoContract]
    public class MsgHundredWeaponsOptProto
    {
        [ProtoMember(1, IsRequired = true)]
        public ActionID Type;

        [ProtoMember(2, IsRequired = true)]
        public LegacyNativeMagicType.WeaponsType WeaponSubtype;

        [ProtoMember(3, IsRequired = true)]
        public int Position;

        [ProtoMember(4, IsRequired = true)]
        public ulong Combination;

        [ProtoMember(5)]
        public byte TriggeredStage;

        [ProtoMember(6)]
        public int TriggeringDuration;

        [ProtoMember(7)]
        public ExchangePositions Exchange;
    }

    [ProtoContract]
    public class ExchangePositions
    {
        [ProtoMember(1, IsRequired = true)]
        public int Position1;

        [ProtoMember(2, IsRequired = true)]
        public int Position2;
    }

    public enum ActionID
    {
        Equip,
        Unequip,
        Swap,
        Combine,
        Trigger
    }
}
