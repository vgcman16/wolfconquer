#nullable disable
using WolfConquer.Core.Networking;
using ItemEffect = WolfConquer.Core.World.LegacyNativeGameItemFlags.ItemEffect;

namespace WolfConquer.Core.World;

// Complete native GetWeaponSpell16..34 only; melee/weapon dispatch is not installed.
internal partial class LegacyNativeGetWeaponSpell
{
    public static void CheckExtraEffects(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
    {
        if (client.Equipment.RingEffect != ItemEffect.None && LegacyNativeAttackBase.Rate(20) &&
            !client.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Stigma))
        {
            LegacyNativeMsgSpellAnimation msgSpellAnimation = new LegacyNativeMsgSpellAnimation(
                client.Player.UID, 0u, client.Player.X, client.Player.Y, 1095, 4, 0);
            client.Player.AddSpellFlag(LegacyNativeMsgUpdate.Flags.Stigma, 20, RemoveOnDead: true);
            msgSpellAnimation.Targets.Enqueue(new LegacyNativeMsgSpellAnimation.SpellObj(
                client.Player.UID, 0u, LegacyNativeMsgAttackPacket.AttackEffect.None));
            msgSpellAnimation.SetStream(stream);
            msgSpellAnimation.Send(client);
        }
        if (client.Equipment.NecklaceEffect != ItemEffect.None && LegacyNativeAttackBase.Rate(20) &&
            !client.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.MagicShield))
        {
            LegacyNativeMsgSpellAnimation msgSpellAnimation2 = new LegacyNativeMsgSpellAnimation(
                client.Player.UID, 0u, client.Player.X, client.Player.Y, 1020, 4, 0);
            client.Player.AddSpellFlag(LegacyNativeMsgUpdate.Flags.MagicShield, 15, RemoveOnDead: true);
            msgSpellAnimation2.Targets.Enqueue(new LegacyNativeMsgSpellAnimation.SpellObj(
                client.Player.UID, 0u, LegacyNativeMsgAttackPacket.AttackEffect.None));
            msgSpellAnimation2.SetStream(stream);
            msgSpellAnimation2.Send(client);
        }
    }
}
