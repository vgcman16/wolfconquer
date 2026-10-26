using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeCalculatePhysical
{
    private static PhysicalMonsterOwners RequireMonsterPhysicalStatus(
        LegacyNativePlayerRuntime player, LegacyNativeMonsterRole monster, LegacyNativeMagicType.Magic? spell)
    {
        var client = player.Owner;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        {
            var binding = LegacyNativeAttackBase.CaptureMinimumMonsterDamageInputs(client);
            if (!ReferenceEquals(binding.Player, player))
                throw UnavailableMonsterPhysicalStatus();
            var owners = CapturePhysicalMonsterOwners(client, monster, spell, binding.MinimumDamage);
            var vector = player.BitVector;
            uint uid = player.UID;
            if (vector is null || !ReferenceEquals(client.Player, player))
                throw UnavailableMonsterPhysicalStatus();

            lock (vector.SyncRoot)
            {
                // Include the exact queries of OnMonster, AjustAttack and AttackEffect.
                // The final OR is the original unnamed numeric index, not a new
                // interpretation as two independent flags.
                ReadOnlySpan<int> queried =
                [
                    (int)LegacyNativeMsgUpdate.Flags.Stigma,
                    (int)LegacyNativeMsgUpdate.Flags.Superman,
                    (int)LegacyNativeMsgUpdate.Flags.FatalStrike,
                    (int)LegacyNativeMsgUpdate.Flags.ManiacDance,
                    (int)LegacyNativeMsgUpdate.Flags.Oblivion,
                    (int)LegacyNativeMsgUpdate.Flags.DeathSigh,
                    (int)LegacyNativeMsgUpdate.Flags.Infinity,
                    (int)LegacyNativeMsgUpdate.Flags.DragonPierceCrit,
                    (int)(LegacyNativeMsgUpdate.Flags.Oblivion | LegacyNativeMsgUpdate.Flags.HawksAmbition),
                ];
                if (!vector.TryValidatePhysicalMonsterStatus(queried) ||
                    !ReferenceEquals(client.Player, player) || player.UID != uid ||
                    !ReferenceEquals(player.BitVector, vector) ||
                    !binding.IsCurrent(client) || !owners.IsCurrent(client, monster, spell))
                    throw UnavailableMonsterPhysicalStatus();
            }
            return owners;
        }
        // Do not hold admission/vector monitors across RNG or SendView. Remaining
        // legacy/raw writers and later publication are not a whole-cast transaction.
    }

    private static InvalidDataException UnavailableMonsterPhysicalStatus() =>
        new("Physical monster calculation requires a current physical player and an available status graph.");
}
