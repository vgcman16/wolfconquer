using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Complete native Player3635..3678 admission methods; no RemoveFlag/ClearFlags substitute.
    internal void AddSpellFlag(LegacyNativeMsgUpdate.Flags Flag, int Secounds, bool RemoveOnDead, int StampSecounds = 0)
    {
        if (BitVector.ContainFlag((int)Flag))
        {
            BitVector.TryRemove((int)Flag);
        }
        AddFlag(Flag, Secounds, RemoveOnDead, StampSecounds);
    }

    internal bool AddFlag(LegacyNativeMsgUpdate.Flags Flag, int Secounds, bool RemoveOnDead,
        int StampSecounds = 0, uint showamount = 0u, uint amount = 0u)
    {
        switch (Flag)
        {
            case LegacyNativeMsgUpdate.Flags.Superman:
            case LegacyNativeMsgUpdate.Flags.Cyclone:
            case LegacyNativeMsgUpdate.Flags.ShurikenVortex:
            case LegacyNativeMsgUpdate.Flags.FatalStrike:
            case LegacyNativeMsgUpdate.Flags.ChaintBolt:
            case LegacyNativeMsgUpdate.Flags.Oblivion:
            case LegacyNativeMsgUpdate.Flags.CannonBarrage:
            case LegacyNativeMsgUpdate.Flags.BlackbeardsRage:
            case LegacyNativeMsgUpdate.Flags.BladeFlurry:
            case LegacyNativeMsgUpdate.Flags.SuperCyclone:
            case LegacyNativeMsgUpdate.Flags.DragonCyclone:
            case LegacyNativeMsgUpdate.Flags.Omnipotence:
            case LegacyNativeMsgUpdate.Flags.ThunderRampage:
                XPBooster = 0;
                Owner.Rune?.IsEquipped("XPBooster", ref XPBooster);
                XPKiller = 0;
                break;
        }
        if (!BitVector.ContainFlag((int)Flag))
        {
            BitVector.TryAdd((int)Flag, Secounds, RemoveOnDead, StampSecounds);
            UpdateFlagOffset();
            if (Flag >= LegacyNativeMsgUpdate.Flags.Accelerated && Flag <= LegacyNativeMsgUpdate.Flags.Confused)
            {
                using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream = recycledPacket.GetStream();
                View.SendView(stream.GameUpdateCreate(UID, (LegacyNativeMsgGameUpdate.DataType)Flag,
                    active: true, showamount, (uint)Secounds, amount), me: true);
            }
            return true;
        }
        return false;
    }
}
