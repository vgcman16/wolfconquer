#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native CheckGemEffects, retaining effect order and short-circuit RNG consumption.
internal class LegacyNativeCheckGemEffects
{
    public enum GemEffect : byte
    {
        moon,
        phoenix,
        rainbow,
        purpleray,
        goldendragon,
        fastflash,
        goldenkylin,
        recovery,
        Aegis4
    }

    public static void CheckRespouseDamage(LegacyNativeGameClientRuntime client)
    {
        if (client.Equipment.SuprtTortoiseGem && LegacyNativeAttackBase.Rate(10) && LegacyNativeAttackBase.Rate(5))
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            client.Player.SendString(stream, LegacyNativeMsgStringPacket.StringID.Effect, true, GemEffect.recovery.ToString());
        }
        if (client.Equipment.HaveBless && LegacyNativeAttackBase.Rate(10) && LegacyNativeAttackBase.Rate(5))
        {
            using (LegacyNativeRecycledPacket recycledPacket2 = new LegacyNativeRecycledPacket())
            {
                LegacyNativePacket stream2 = recycledPacket2.GetStream();
                client.Player.SendString(stream2, LegacyNativeMsgStringPacket.StringID.Effect, true, GemEffect.Aegis4.ToString());
            }
        }
    }

    public static void TryngEffect(LegacyNativeGameClientRuntime client)
    {
        if (client.Equipment.SuperDragonGem && LegacyNativeAttackBase.Rate(4))
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            client.Player.SendString(stream, LegacyNativeMsgStringPacket.StringID.Effect, true, GemEffect.goldendragon.ToString());
        }
        if (client.Equipment.SuperPheonixGem && LegacyNativeAttackBase.Rate(5))
        {
            using LegacyNativeRecycledPacket recycledPacket2 = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream2 = recycledPacket2.GetStream();
            client.Player.SendString(stream2, LegacyNativeMsgStringPacket.StringID.Effect, true, GemEffect.phoenix.ToString());
        }
        if (client.Equipment.SuperRaibowGem && LegacyNativeAttackBase.Rate(5))
        {
            using LegacyNativeRecycledPacket recycledPacket3 = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream3 = recycledPacket3.GetStream();
            client.Player.SendString(stream3, LegacyNativeMsgStringPacket.StringID.Effect, true, GemEffect.rainbow.ToString());
        }
        if (client.Equipment.SuperMoonGem && LegacyNativeAttackBase.Rate(5))
        {
            using LegacyNativeRecycledPacket recycledPacket4 = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream4 = recycledPacket4.GetStream();
            client.Player.SendString(stream4, LegacyNativeMsgStringPacket.StringID.Effect, true, GemEffect.moon.ToString());
        }
        if (client.Equipment.SuperVioletGem && LegacyNativeAttackBase.Rate(5))
        {
            using (LegacyNativeRecycledPacket recycledPacket5 = new LegacyNativeRecycledPacket())
            {
                LegacyNativePacket stream5 = recycledPacket5.GetStream();
                client.Player.SendString(stream5, LegacyNativeMsgStringPacket.StringID.Effect, true, GemEffect.purpleray.ToString());
            }
        }
    }
}
