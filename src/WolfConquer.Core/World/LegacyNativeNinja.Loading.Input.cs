namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed partial class LoadingInput
    {
        private readonly LegacyNativeNinja ninja;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeEquipItemStorage equipment;
        private readonly uint uid, job;
        private readonly bool alternate, fake, tracksBadge;
        private readonly object? socket;
        private readonly PersistenceState state;
        private readonly RankingItem[] rows;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? table;
        private readonly SkillMetadata[] metadata;

        internal uint Uid => uid;
        internal byte PreviousBadge { get; }
        internal byte Badge { get; }
        internal bool BadgeChanges => tracksBadge && PreviousBadge != Badge;
        internal LegacyNativeSpellStorage.NinjaSkillChoice[] Skills { get; }
        internal LegacyNativeMsgNinja.MsgNinjaUser UserMessage { get; private set; } = null!;
        internal LegacyNativeMsgNinja.MsgNinjaItem ItemMessage { get; private set; } = null!;

        internal LoadingInput(LegacyNativeNinja ninja, LegacyNativeGameClientRuntime client,
            LegacyNativePlayerRuntime player, bool skills, bool badge, bool messages)
        {
            this.ninja = ninja; this.client = client; this.player = player;
            uid = player.UID; job = player.Class;
            if (uid == 0 || !ninja.IsCombatOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || client.Equipment is not { } owned || !owned.IsOwnedBy(client))
                throw UnavailableRanking();
            equipment = owned; alternate = equipment.Alternante;
            state = ninja.ReadPersistenceState();
            rows = CaptureRankingItems(state.Items);
            var selected = SelectLoadingItems(rows, alternate);
            tracksBadge = badge;
            PreviousBadge = player.NiniaP0; Badge = badge ? PrepareLoadingBadge(selected) : PreviousBadge;
            if (skills) (table, metadata) = CaptureLoadingMetadata(selected);
            else { table = null; metadata = []; }
            Skills = skills ? PrepareLoadingSkills(selected, metadata) : [];
            socket = client.Socket; fake = client.Fake;
            if (messages) PrepareMessages();
            RequireCurrent();
        }

        internal void RequireCurrent()
        {
            RequireOwner();
            if (tracksBadge && player.NiniaP0 != PreviousBadge) throw UnavailableRanking();
        }

        private void RequireOwner()
        {
            if (!ninja.IsCombatOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || player.UID != uid || player.Class != job ||
                !ReferenceEquals(ninja.ReadPersistenceState(), state) || !ReferenceEquals(client.Equipment, equipment) ||
                !equipment.IsOwnedBy(client) || equipment.Alternante != alternate) throw UnavailableRanking();
            RequireCurrentRankingItems(state.Items, rows);
            RequireLoadingMetadata(table, metadata);
            if (scoreTable is not null) RequireCurrentRankingMetadata(scoreTable, scoreRows);
        }

        internal bool HasCommitted(bool badgeApplied)
        {
            if (!ReferenceEquals(client.Socket, socket) || client.Fake != fake ||
                tracksBadge && player.NiniaP0 != (badgeApplied ? Badge : PreviousBadge)) return false;
            try { RequireOwner(); return true; }
            catch (InvalidOperationException) { return false; }
        }

        internal void PublishPacket(WolfConquer.Core.Networking.LegacyNativePacket packet) => client.Send(packet);
    }
}
