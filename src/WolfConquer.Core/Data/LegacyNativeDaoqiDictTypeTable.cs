#nullable disable

using System;
using System.Collections.Generic;

namespace WolfConquer.Core.Data;

// Complete native CQ_DAOQI_DICT_TYPE row/enums/cache; its loader is in the other partial.
internal partial class LegacyNativeDaoqiDictTypeTable
{
    public class CQ
    {
        public uint ID;

        public Type Type;

        public uint attr_type1;

        public uint attr_value1;

        public uint attr_type2;

        public uint attr_value2;

        public uint score;

        public uint points;

        public uint rate;

        public uint TypeRune;

        public uint Level;

        public uint Exp;

        public uint u12;

        public uint u13;

        public uint TypeCombatGear;
    }

    [Flags]
    public enum Type : uint
    {
        Refined = 0u,
        Elite = 1u,
        Super = 2u,
        Sacred = 3u
    }

    [Flags]
    public enum AttrType : uint
    {
        None = 0u,
        MaxHP = 1u,
        PAttack = 2u,
        PDefense = 3u,
        MAttack = 4u,
        MDefense = 5u,
        FinalPAttack = 6u,
        FinalPDamage = 7u,
        FinalMAttack = 8u,
        FinalMDamage = 9u,
        PStrike = 0xAu,
        MStrike = 0xBu,
        Immunity = 0xCu,
        Break = 0xDu,
        AntiBreak = 0xEu,
        Parry = 0xFu,
        LuckyStrike = 0x10u,
        CoreStrike = 0x11u,
        InvisibleArrow = 0x12u,
        DrainingTouch = 0x13u,
        BloodSpawn = 0x14u,
        KillingFlash = 0x15u,
        MirrorOfSin = 0x16u,
        MaxMP = 0x17u,
        Spell = 0x18u
    }

    public static Dictionary<uint, CQ> Info
    {
        get => LegacyNativeCombatGearTable.MailboxJade;
        set => LegacyNativeCombatGearTable.MailboxJade = value;
    }
}
