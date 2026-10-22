namespace WolfConquer.Core.World;

// Native MsgAttackPacket19..74 complete enum/reference state; no attack dispatcher is installed.
internal static partial class LegacyNativeMsgAttackPacket
{
    [Flags]
    public enum AttackEffect : uint
    {
        None = 0u,
        Block = 1u,
        Penetration = 2u,
        CriticalStrike = 4u,
        Imunity = 8u,
        Break = 0xAu,
        ResistMetal = 0x10u,
        ResistWood = 0x20u,
        ResistWater = 0x40u,
        ResistFire = 0x80u,
        ResistEarth = 0x100u,
        AddStudyPoint = 0x200u,
        LuckyStrike = 0x400u,
        NatureShield = 0x2000u,
        Assasssin = 0x4000u,
        BurningSky = 0x8000u,
        FireCurse = 0x10000u,
        BonePulseDestroy = 0x4000000u,
        TailedBeast = 0x40000u,
        Destroy = 0x1000000u,
        Dash = 0x8000000u
    }

    public enum AttackID : uint
    {
        None = 0u,
        Physical = 2u,
        Magic = 24u,
        Archer = 28u,
        RequestMarriage = 8u,
        AcceptMarriage = 9u,
        Death = 14u,
        Reflect = 26u,
        Dash = 27u,
        UpdateHunterJar = 36u,
        CounterKillSwitch = 44u,
        Scapegoat = 43u,
        FatalStrike = 45u,
        InteractionRequest = 46u,
        InteractionAccept = 47u,
        InteractionRefuse = 48u,
        InteractionEffect = 49u,
        InteractionStopEffect = 50u,
        InMoveSpell = 53u,
        BlueDamage = 55u,
        BackFire = 57u
    }

    public class AttackObj
    {
        public LegacyNativeGameClientRuntime? User;
        public LegacyNativeInteractQuery? Attack;
    }
}
