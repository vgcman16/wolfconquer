using System.Collections.Concurrent;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    // Source GameClient1387 genuine constructor owner + ServerDatabase1116 Load.
    // Prepare detached and validate completely; never bind temporarily to satisfy
    // IsCombatOwnedBy or call Loading (ranking/equipment/sends) during recovery.
    internal static bool TryPrepareMailboxRecoveryOwner(LegacyNativeGameClientRuntime client,
        CharacterSnapshot snapshot, out MailboxRecoveryOwner? prepared)
    {
        prepared = null;
        if (client?.Player is not { } player || snapshot is null || !ReferenceEquals(player.Owner, client) ||
            client.FullLoading || client.Fake || client.MyNinja is not null || snapshot.Uid is <= 0 or > uint.MaxValue ||
            snapshot.Class is < 0 or > uint.MaxValue || !LegacyNativeAtributesStatus.IsNinja((uint)snapshot.Class) ||
            player.UID != 0 && player.UID != snapshot.Uid || player.Class != 0 && player.Class != snapshot.Class ||
            snapshot.Attributes is null || !snapshot.Attributes.TryGetValue("MsgNinja", out var line) ||
            !snapshot.Attributes.TryGetValue("ClassExperience", out var rawExperience) ||
            !uint.TryParse(rawExperience, NumberStyles.None, CultureInfo.InvariantCulture, out var experience) ||
            !MailboxRecoveryLine.TryRead(line, out var parsed)) return false;
        var candidate = new LegacyNativeNinja(client);
        candidate.Load(line); // Strict validated source input; no live publication or side effects.
        var projection = new MailboxRecoveryOwner(client, player, candidate, parsed!, snapshot, experience, line);
        if (!projection.IsDetachedCurrent()) return false;
        prepared = projection;
        return true;
    }

    internal sealed class MailboxRecoveryOwner
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeNinja candidate;
        private readonly MailboxRecoveryLine parsed;
        private readonly ConcurrentDictionary<uint, Item> items;
        private readonly KeyValuePair<uint, Item>[] rows;
        private readonly uint uid, job, experience;
        private readonly string line;
        private bool published;

        internal MailboxRecoveryOwner(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
            LegacyNativeNinja candidate, MailboxRecoveryLine parsed, CharacterSnapshot snapshot, uint experience, string line)
        {
            this.client = client; this.player = player; this.candidate = candidate; this.parsed = parsed;
            uid = checked((uint)snapshot.Uid); job = checked((uint)snapshot.Class); this.experience = experience; this.line = line;
            items = candidate.Items; rows = items.ToArray();
        }

        private bool IdentityCurrent() => !client.FullLoading && !client.Fake && ReferenceEquals(client.Player, player) &&
            ReferenceEquals(player.Owner, client) && ReferenceEquals(candidate.combatConstructorOwner, client) &&
            ReferenceEquals(candidate.user, client);

        internal bool IsDetachedCurrent() => !published && client.MyNinja is null && IdentityCurrent() &&
            parsed.Matches(candidate, items, rows);

        private bool LoadedPlayerCurrent() => player.UID == uid && player.Class == job && player.ClassExperience == experience;

        internal bool TryPublish(WorldSessionStore sessions, ConquerTcpConnection connection, WorldSession admitted)
        {
            var applied = false;
            sessions.WithCurrentNativeClientIdentity(connection, uid, _ =>
            {
                if (!sessions.TryGet(connection, out var current) || !ReferenceEquals(current, admitted) ||
                    !ReferenceEquals(connection.NativeGameClient, client) || !ReferenceEquals(admitted.NativeGameClient, client) ||
                    admitted.Character.Uid != uid || admitted.Character.Class != job || admitted.Character.Attributes is null ||
                    !admitted.Character.Attributes.TryGetValue("MsgNinja", out var saved) || saved != line ||
                    !admitted.Character.Attributes.TryGetValue("ClassExperience", out var raw) ||
                    !uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var exp) || exp != experience ||
                    !IsDetachedCurrent() || !PublicationIdentityCurrent(sessions, connection, admitted)) return;
                client.MyNinja = candidate;
                published = true;
                // This callback-free slot assignment is the publication point.
                // A later current-state query may reject delivery, not undo or relabel
                // this already-performed one-way adoption as an empty rejection.
                applied = true;
            });
            return applied;
        }

        // All caller-owned attributes/collection reads precede this final fence.
        // Store mutation reentry during preparation is refused. The earlier
        // holder check still cannot replace this callback-free final identity fence.
        private bool PublicationIdentityCurrent(WorldSessionStore sessions, ConquerTcpConnection connection, WorldSession admitted) =>
            sessions.TryGet(connection, out var current) && ReferenceEquals(current, admitted) &&
            ReferenceEquals(connection.NativeGameClient, client) && ReferenceEquals(admitted.NativeGameClient, client) &&
            admitted.Character.Uid == uid && admitted.Character.Class == job &&
            !published && client.MyNinja is null && IdentityCurrent() && LoadedPlayerCurrent();

        internal bool IsPublishedCurrent() => published && IdentityCurrent() && LoadedPlayerCurrent() &&
            ReferenceEquals(client.MyNinja, candidate) && candidate.IsCombatOwnedBy(client) && parsed.Matches(candidate, items, rows);
    }
}
