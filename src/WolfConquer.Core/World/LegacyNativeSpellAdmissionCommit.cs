namespace WolfConquer.Core.World;

// Engineering primitive, not a replacement native owner or a simulated resource setter.
internal readonly record struct LegacyNativeSpellAdmissionCommit(
    ushort Stamina, uint Mana, ushort SoulSpell, bool ConsumeWeaponAuthorization,
    LegacyNativeMsgSpell ClientSpell, bool StartCooldown, DateTime ColdTime);

public sealed partial class LegacyNativeGameClientRuntime
{
    // Always-present synchronization independent of UID, sockets, and native worker birth.
    internal object SpellAdmissionSyncRoot { get; } = new();
}

public sealed partial class LegacyNativePlayerRuntime
{
    internal object SpellAdmissionSyncRoot { get; } = new();

    // Caller holds client->player monitors and has completed all fallible preparation.
    // Owned managed Frame casts use their immutable Store publication instead.
    internal void CommitSpellAdmission(LegacyNativeGameClientRuntime client,
        in LegacyNativeSpellAdmissionCommit commit)
    {
        if (!ReferenceEquals(Owner, client) || !ReferenceEquals(client.Player, this) ||
            !Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(SpellAdmissionSyncRoot) ||
            HasOwnedVitalsAttachment)
            throw new InvalidOperationException("nativeCompoundSpellRequiresCompleteOwnedPlan");
        Stamina = commit.Stamina;
        _mana = commit.Mana;
        if (commit.ConsumeWeaponAuthorization)
            RandomSpell = 0;
        client.OnSoulSpell = commit.SoulSpell;
        if (commit.StartCooldown)
        {
            commit.ClientSpell.IsSpellWithColdTime = true;
            commit.ClientSpell.ColdTime = commit.ColdTime;
        }
    }
}
