using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Sanitized native spell admission: preparation is read-only; commitment is serialized.
// Executor-specific target/class/effect admission remains the executor's responsibility.
internal partial class LegacyNativeCanUseSpell
{
    public static bool Verified(LegacyNativeInteractQuery Attack, LegacyNativeGameClientRuntime client,
        Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells, out LegacyNativeMsgSpell ClientSpell,
        out LegacyNativeMagicType.Magic Spell)
    {
        ClientSpell = null!;
        Spell = null!;
        if (Attack is null || client is null || DBSpells is null)
            return false;

        lock (client.SpellAdmissionSyncRoot)
        {
            LegacyNativePlayerRuntime? player = client.Player;
            if (player is null || !ReferenceEquals(player.Owner, client))
                return false;

            lock (player.SpellAdmissionSyncRoot)
            {
                AdmissionPlan? plan;
                PreparedUpdates? updates = null;
                try
                {
                    if (!TryPrepare(Attack, client, player, DBSpells, out plan))
                        return false;
                    updates = PreparedUpdates.Create(player.UID, plan!);
                    // A lifecycle writer not participating in admission must not cause
                    // a prepared cost to be applied to a different physical Player.
                    if (!ReferenceEquals(client.Player, player) ||
                        plan!.SigilCapture is { } sigilCapture && !sigilCapture.IsCurrent())
                    {
                        updates.Dispose();
                        return false;
                    }
                }
                catch (Exception error)
                {
                    updates?.Dispose();
                    Report("preparation", error);
                    return false;
                }

                // No allocation, virtual call, resource setter, serialization, or
                // publication is allowed inside this strongly typed commit.
                player.CommitSpellAdmission(client, plan!.Commit);
                ClientSpell = plan.Commit.ClientSpell;
                Spell = plan.DatabaseSpell;
                try
                {
                    updates!.Publish(client);
                }
                catch (Exception error)
                {
                    // The cast is already accepted. Transport failure must not turn
                    // it into a rejection after costs/authorization have committed.
                    Report("publication after commit", error);
                }
                finally
                {
                    updates!.Dispose();
                }
                return true;
            }
        }
    }

    private sealed record AdmissionPlan(
        LegacyNativeMagicType.Magic DatabaseSpell,
        LegacyNativeSpellAdmissionCommit Commit,
        bool SendStamina, bool SendMana, LegacyNativeNinja.EquippedSigilCapture? SigilCapture);

    private static bool TryPrepare(LegacyNativeInteractQuery attack,
        LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        Dictionary<ushort, LegacyNativeMagicType.Magic> database,
        out AdmissionPlan? plan)
    {
        plan = null;
        if (!TryAuthorizeWeapon(attack.SpellID, player, out bool consumeWeaponAuthorization) ||
            client.MySpells?.ClientSpells is not { } learnedSpells ||
            !learnedSpells.TryGetValue(attack.SpellID, out var learned) || learned is null ||
            !database.TryGetValue(learned.Level, out var row) || row is null)
            return false;

        if (!HasConsistentIdentity(attack.SpellID, learned, row))
            return false;

        // MagicType.Load shares6010's level dictionary with alias6009. IDs need
        // not be identical; both returned objects remain the actual server-owned rows.
        if ((row.Type == LegacyNativeMagicType.MagicSort.DirectAttack ||
             row.Type == LegacyNativeMagicType.MagicSort.Attack) &&
            !client.IsInSpellRange(attack.OpponentUID, row.Range))
            return false;
        if (!TryPrepareResources(client, player, row, out int staminaCost, out int manaCost) ||
            !TryPrepareCooldown(client, player, learned, row, out bool startCooldown, out DateTime deadline, out var sigilCapture))
            return false;

        plan = new AdmissionPlan(row,
            new LegacyNativeSpellAdmissionCommit(
                (ushort)(player.Stamina - staminaCost), (ushort)(player.Mana - manaCost),
                learned.UseSpellSoul > 0 ? learned.ID : (ushort)0,
                consumeWeaponAuthorization, learned, startCooldown, deadline),
            staminaCost > 0, manaCost > 0 && client.FullLoading, sigilCapture);
        return true;
    }

    private static bool HasConsistentIdentity(ushort requestedId,
        LegacyNativeMsgSpell learned, LegacyNativeMagicType.Magic row) =>
        learned.ID == requestedId && row.Level == learned.Level && row.ColdTime >= 0 &&
        (row.ID == requestedId || (requestedId == 6009 && row.ID == 6010));

    private static bool TryAuthorizeWeapon(ushort spellId, LegacyNativePlayerRuntime player,
        out bool consumeAuthorization)
    {
        consumeAuthorization = false;
        var weaponSpells = LegacyNativePoolMagic.WeaponSpells;
        if (weaponSpells is null)
            return false;
        foreach (var entry in weaponSpells)
        {
            if (entry.Value is null)
                return false;
            if (!entry.Value.Contains(spellId))
                continue;
            if (player.RandomSpell != spellId)
                return false;
            consumeAuthorization = true;
            break;
        }
        return true;
    }

    private static void Report(string phase, Exception error)
    {
        try { Console.WriteLine($"Spell admission {phase}: {error}"); }
        catch { /* Diagnostic sink failure cannot change a committed cast's result. */ }
    }
}
