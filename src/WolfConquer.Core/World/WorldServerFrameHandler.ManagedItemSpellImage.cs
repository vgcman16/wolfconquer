using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool MatchesManagedItemSpellImage(WorldSessionStore store, ConquerTcpConnection connection,
        WorldSession expected, WorldSession stable, string path, byte[] before)
    {
        try
        {
            var origin = RequireUnboundCharacterOrigin(store, connection, expected);
            var witness = new LegacyManagedCharacterImage(stable.Character);
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(origin.Path),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                !origin.Image.AsSpan().SequenceEqual(before) || !witness.IsCurrent()) return false;
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(before, out _));
            return LegacyManagedCharacterImage.MatchesSaved(saved, stable.Character, origin.RuntimeVigor);
        }
        catch (InvalidOperationException) { return false; }
    }

    internal void CompleteManagedItemSpellImage(WorldSessionStore store, ConquerTcpConnection connection,
        WorldSession expected, WorldSession published, LegacyPreparedSpellFileWrite writer)
    {
        var origin = RequireUnboundCharacterOrigin(store, connection, published);
        var receipt = writer.RequireManagedCharacterCommit(store, connection, expected, published);
        var before = receipt.CopyCharacterBefore();
        var after = receipt.CopyCharacterAfter();
        if (!string.Equals(Path.GetFullPath(receipt.CharacterPath), Path.GetFullPath(origin.Path),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
            !origin.Image.AsSpan().SequenceEqual(before))
            throw new InvalidOperationException("managedItemSpellOriginChangedAfterCommit");
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(after, out _));
        var inputs = new LegacyManagedCharacterImage(published.Character);
        if (!inputs.IsCurrent() || !LegacyManagedCharacterImage.MatchesSaved(saved, published.Character, origin.RuntimeVigor))
            throw new InvalidOperationException("managedItemSpellPublishedImageMismatch");
        origin.Image = after;
    }
}
