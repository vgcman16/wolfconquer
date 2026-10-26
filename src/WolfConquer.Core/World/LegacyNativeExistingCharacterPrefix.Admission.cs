using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeExistingCharacterPrefix
{
    internal bool IsCurrentForAdmission() => IsCurrent();
    internal byte[] CopyAdmissionBeforeImage() => before.ToArray();
    internal CharacterSnapshot AdmissionSnapshot => updated;
    internal bool IsPublishedAdmissionOwnerCurrent() => published && ReferenceEquals(connection.NativeGameClient, client) &&
        ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) && !client.FullLoading && !client.Fake &&
        player.UID == updated.Uid && ReferenceEquals(client.Beasts, beasts.Target) && ReferenceEquals(client.HairfaceStorage, hairface.Target);
    internal static bool AdmissionMatchesSnapshot(CharacterSnapshot expected, byte[] image, string path) => SnapshotMatches(expected, image, path);

    // Only the genuine enclosing admission transaction calls this after its
    // verified durable joint commit. It supplies no saver or accepting hook.
    internal void PublishCommittedAdmission()
    {
        playerLoad.PublishPrepared(); hairface.PublishPrepared(); beasts.PublishPrepared();
        published = true;
    }
}
