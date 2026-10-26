using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Source ServerDatabase.LoadCharacter777/784..789 and Player.Class1592:
    // while !FullLoading the actual class setter only assigns _class. This is a
    // bounded cold saved-state load, not a partial public/live Class setter.
    internal bool TryPrepareMailboxRecoveryLoad(LegacyNativeGameClientRuntime client, CharacterSnapshot snapshot,
        out MailboxRecoveryLoad? prepared)
    {
        prepared = null;
        if (snapshot is null || client is null || !ReferenceEquals(Owner, client) || !ReferenceEquals(client.Player, this) ||
            client.FullLoading || client.Fake || snapshot.Uid is <= 0 or > uint.MaxValue ||
            snapshot.Class is < 0 or > uint.MaxValue || snapshot.Body is < 0 or > ushort.MaxValue ||
            snapshot.Face is < 0 or > ushort.MaxValue || UID != 0 && UID != snapshot.Uid || snapshot.Attributes is null ||
            !snapshot.Attributes.TryGetValue("ClassExperience", out var raw) ||
            !uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var exp) ||
            snapshot.Name is null || Name is null || Spouse is null) return false;
        // Exact native reader fallback for the optional saved spouse field.
        var spouse = snapshot.Attributes.TryGetValue("Spouse", out var savedSpouse) ? savedSpouse : "None";
        if (spouse is null) return false;
        prepared = new(this, client, snapshot, exp, spouse);
        return prepared.IsCurrent();
    }

    internal sealed class MailboxRecoveryLoad
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly uint uid, job, exp, oldUid, oldJob, oldExp;
        private readonly ushort newBody, newFace, oldBody, oldFace, oldTransformation;
        private readonly string newName, newSpouse, oldName, oldSpouse;
        private bool published;

        internal MailboxRecoveryLoad(LegacyNativePlayerRuntime player, LegacyNativeGameClientRuntime client,
            CharacterSnapshot source, uint exp, string spouse)
        {
            this.player = player; this.client = client; uid = checked((uint)source.Uid); job = checked((uint)source.Class);
            this.exp = exp; newBody = checked((ushort)source.Body); newFace = checked((ushort)source.Face);
            newName = source.Name; newSpouse = spouse;
            oldUid = player.UID; oldJob = player._class; oldExp = player._classexp;
            oldBody = player.body; oldFace = player.face; oldTransformation = player._transformationid;
            oldName = player.Name; oldSpouse = player.Spouse;
        }

        internal bool IsCurrent() => !published && Reciprocal() && player.UID == oldUid && player._class == oldJob &&
            player._classexp == oldExp && player.body == oldBody && player.face == oldFace &&
            player._transformationid == oldTransformation && player.Name == oldName && player.Spouse == oldSpouse;

        private bool Reciprocal() => ReferenceEquals(player.Owner, client) && ReferenceEquals(client.Player, player) &&
            !client.FullLoading && !client.Fake;

        internal bool TryPublish(WorldSessionStore sessions, ConquerTcpConnection connection, WorldSession admitted)
        {
            if (!ReferenceEquals(connection.NativeGameClient, client) || !ReferenceEquals(admitted.NativeGameClient, client) ||
                admitted.Character.Uid != uid || admitted.Character.Class != job || admitted.Character.Name != newName ||
                admitted.Character.Body != newBody || admitted.Character.Face != newFace ||
                admitted.Character.Attributes is null ||
                (admitted.Character.Attributes.TryGetValue("Spouse", out var spouse) ? spouse : "None") != newSpouse ||
                !admitted.Character.Attributes.TryGetValue("ClassExperience", out var text) ||
                !uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var savedExp) || savedExp != exp) return false;
            var applied = false;
            sessions.WithCurrentNativeClientIdentity(connection, uid, _ =>
            {
                if (!sessions.TryGet(connection, out var current) || !ReferenceEquals(current, admitted) || !IsCurrent()) return;
                // Cold branches only: no setters that send, guild/Gear refresh,
                // save, allocation, owner fabrication, or FullLoading toggle.
                player.UID = uid; player._class = job; player._classexp = exp;
                player.body = newBody; player.face = newFace; player.Name = newName; player.Spouse = newSpouse;
                published = true;
                applied = IsPublishedCurrent();
            });
            return applied;
        }

        internal bool IsPublishedCurrent() => published && Reciprocal() && player.UID == uid && player._class == job &&
            player._classexp == exp && player.body == newBody && player.face == newFace &&
            player._transformationid == oldTransformation && player.Name == newName && player.Spouse == newSpouse;
    }
}
