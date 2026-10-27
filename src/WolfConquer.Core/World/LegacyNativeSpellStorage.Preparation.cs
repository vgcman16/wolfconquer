namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private readonly object mutationGate = new();
    internal object MutationGate => mutationGate;
    internal bool IsOwnedBy(LegacyNativeGameClientRuntime client) => client is not null &&
        ReferenceEquals(Owner, client) && ReferenceEquals(client.MySpells, this);

    internal readonly record struct NinjaSkillChoice(ushort Id, uint Level);

    internal PreparedNinjaSpells PrepareNinjaRefresh(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, uint uid, NinjaSkillChoice[] choices, ushort[] managedSkills)
    {
        RequireNinjaSpellOwner(client, player, uid);
        if (choices is null || choices.Length > 16 || managedSkills is null || managedSkills.Length != 25)
            throw UnavailableNinjaSpells(); // Eight selected positions, two actual skill fields each.
        return new(this, client, player, uid, choices, managedSkills);
    }

    private void RequireNinjaSpellOwner(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, uint uid)
    {
        if (client is null || player is null || uid == 0 || !IsOwnedBy(client) ||
            !ReferenceEquals(client.Player, player) || !ReferenceEquals(player.Owner, client) || player.UID != uid ||
            !Monitor.IsEntered(client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(player.SpellAdmissionSyncRoot) ||
            !Monitor.IsEntered(mutationGate)) throw UnavailableNinjaSpells();
    }

    private static bool PermittedNinjaSkill(ushort id) => id != 1105 && (id < 4000 || id > 4070);
    private static InvalidOperationException UnavailableNinjaSpells() =>
        new("Spell preparation requires a complete current physical spell owner and graph.");
}
