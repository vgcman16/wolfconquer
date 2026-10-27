namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private sealed class PreparedStatusRankBonus
    {
        private readonly LegacyNativeMyCombatGear gear;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeSpellStorage spells;
        private readonly LegacyNativeMsgStatus status;
        private readonly LegacyNativePerfectionEffect perfection;
        private readonly uint uid, job;
        private readonly RankBonusValues before, after;

        internal PreparedStatusRankBonus(LegacyNativeMyCombatGear gear,
            LegacyNativeSpellStorage spells, VRANK rank)
        {
            this.gear = gear;
            this.spells = spells;
            client = gear.user ?? throw UnavailableStatusRankBonus();
            player = client.Player ?? throw UnavailableStatusRankBonus();
            status = client.Status ?? throw UnavailableStatusRankBonus();
            perfection = client.PerfectionStatus ?? throw UnavailableStatusRankBonus();
            uid = player.UID;
            job = player.Class;
            if (rank is null || uid == 0 || spells is null || spells.ClientSpells is null ||
                !spells.IsOwnedBy(client) || !ReferenceEquals(player.Owner, client))
                throw UnavailableStatusRankBonus();
            before = ReadValues();
            // Exact six A cases: integer /100 and unchecked uint additions remain.
            after = new(
                before.LuckyStrike + NativeCombatStatusInt32(rank.CoreStrike) / 100,
                before.StrikeLock + NativeCombatStatusInt32(rank.Parry) / 100,
                before.CoreStrike + NativeCombatStatusInt32(rank.CoreStrike) / 100,
                unchecked(before.MinAttack + NativeCombatStatusUInt32(rank.PAttack)),
                unchecked(before.MaxAttack + NativeCombatStatusUInt32(rank.PAttack)),
                unchecked(before.Defence + NativeCombatStatusUInt32(rank.PDefense)),
                unchecked(before.MagicDefence + NativeCombatStatusUInt32(rank.MDefense)));
            if (!HasCurrentTargets()) throw UnavailableStatusRankBonus();
        }

        private RankBonusValues ReadValues() => new(perfection.LuckyStrike,
            perfection.StrikeLock, perfection.CoreStrike, status.MinAttack,
            status.MaxAttack, status.Defence, status.MagicDefence);

        private bool HasCurrentTargets() =>
            ReferenceEquals(gear.user, client) && ReferenceEquals(client.Player, player) &&
            ReferenceEquals(player.Owner, client) && player.UID == uid && player.Class == job &&
            ReferenceEquals(client.MySpells, spells) && spells.IsOwnedBy(client) &&
            ReferenceEquals(client.Status, status) && ReferenceEquals(client.PerfectionStatus, perfection) &&
            ReadValues() == before;

        internal bool TryApply()
        {
            // A spell may already be committed. Stale follow-on work stops without
            // claiming rejection or undoing that committed spell notification.
            if (!HasCurrentTargets()) return false;
            perfection.LuckyStrike = after.LuckyStrike;
            perfection.StrikeLock = after.StrikeLock;
            perfection.CoreStrike = after.CoreStrike;
            status.MinAttack = after.MinAttack;
            status.MaxAttack = after.MaxAttack;
            status.Defence = after.Defence;
            status.MagicDefence = after.MagicDefence;
            return true;
        }
    }

    private readonly record struct RankBonusValues(double LuckyStrike, double StrikeLock,
        double CoreStrike, uint MinAttack, uint MaxAttack, uint Defence, uint MagicDefence);

    private static InvalidOperationException UnavailableStatusRankBonus() =>
        new("Combat-gear rank bonuses require current physical status and spell owners.");
}
