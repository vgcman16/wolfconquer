using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyDragonSkinMyShowAsync(WorldPacketHandlerContext context,
        WorldSession session, CancellationToken cancellationToken)
    {
        if (!TryCaptureDragonSkinOwner(context, session, out var owner, out var reason))
        { await LogDragonSkinRefusalAsync(context, reason).ConfigureAwait(false); return; }
        var fields = owner.Book.CopyFields();
        var loaded = ReadAttributeBool(fields, RuntimeAnimaSkinLoadedAttribute);
        var frames = new List<byte[]>();
        long preparedBytes = 0;
        if (!loaded)
            foreach (var skin in owner.Book.State.OwnedSkins)
            {
                var bytes = new LegacyDragonSkinPacket(LegacyDragonSkinType.Unlock, (uint)session.Character.Uid, skin, true).ToFrameBytes();
                preparedBytes += bytes.Length;
                if (preparedBytes > CharacterSnapshotFileBaseline.MaximumBytes)
                { await LogDragonSkinRefusalAsync(context, "dragonSkinPresentationTooLarge").ConfigureAwait(false); return; }
                frames.Add(bytes);
            }
        var show = new LegacyDragonSkinPacket(LegacyDragonSkinType.MyShow, (uint)session.Character.Uid,
            owner.Book.State.EquippedSkin, false).ToFrameBytes();
        foreach (var bytes in frames)
            if (!await SendCurrentDragonSkinFrameAsync(context, owner, session, bytes, cancellationToken).ConfigureAwait(false)) return;
        var expected = session;
        if (!loaded)
        {
            fields[RuntimeAnimaSkinLoadedAttribute] = "1";
            expected = session with { Character = session.Character with { Attributes = fields } };
            if (!TryCommitDragonSkinPresentation(context, owner, expected, out expected, out reason))
            { await LogDragonSkinRefusalAsync(context, reason).ConfigureAwait(false); return; }
        }
        try
        {
            if (!await SendCurrentDragonSkinFrameAsync(context, owner, expected, show, cancellationToken).ConfigureAwait(false)) return;
            await context.Log.WriteLineAsync($"WorldServer sent LegacyDragonSkin MyShow {FormatSession(context)} " +
                $"skinType={owner.Book.State.EquippedSkin} unlocks={frames.Count}.").ConfigureAwait(false);
        }
        catch (Exception) when (!loaded) { /* Runtime marker was already committed; never undo it or older items. */ }
    }

    private static async ValueTask HandleLegacyDragonSkinSelectAsync(WorldPacketHandlerContext context,
        LegacyDragonSkinPacket packet, WorldSession session, CancellationToken cancellationToken)
    {
        if (!TryCaptureDragonSkinOwner(context, session, out var owner, out var reason))
        { await LogDragonSkinRefusalAsync(context, reason).ConfigureAwait(false); return; }
        if (!owner.Book.State.Owns(packet.SkinType))
        {
            await context.Log.WriteLineAsync($"WorldServer skipped LegacyDragonSkin Select {FormatSession(context)} " +
                $"skinType={packet.SkinType}; skin is not owned.").ConfigureAwait(false);
            return;
        }
        var fields = owner.Book.CopyFields();
        fields[LegacyAnimaSkinAttribute] = owner.Book.State.WithEquipped(packet.SkinType).ToSnapshotValue();
        var updated = session with { Character = session.Character with { Attributes = fields } };
        var frame = new LegacyDragonSkinPacket(LegacyDragonSkinType.MyShow, (uint)session.Character.Uid, packet.SkinType, false).ToFrameBytes();
        if (!TryCommitDragonSkinPresentation(context, owner, updated, out updated, out reason))
        { await LogDragonSkinRefusalAsync(context, reason).ConfigureAwait(false); return; }
        try
        {
            if (!await SendCurrentDragonSkinFrameAsync(context, owner, updated, frame, cancellationToken).ConfigureAwait(false)) return;
            await context.Log.WriteLineAsync($"WorldServer selected LegacyDragonSkin {FormatSession(context)} skinType={packet.SkinType}.").ConfigureAwait(false);
        }
        catch (Exception) { /* Committed presentation is not a rejected entitlement transaction. */ }
    }

    private static async ValueTask HandleLegacyDragonSkinRestoreAsync(WorldPacketHandlerContext context,
        WorldSession session, CancellationToken cancellationToken)
    {
        if (!TryCaptureDragonSkinOwner(context, session, out var owner, out var reason))
        { await LogDragonSkinRefusalAsync(context, reason).ConfigureAwait(false); return; }
        var fields = owner.Book.CopyFields();
        fields[LegacyAnimaSkinAttribute] = owner.Book.State.Restore().ToSnapshotValue();
        var updated = session with { Character = session.Character with { Attributes = fields } };
        var frame = new LegacyDragonSkinPacket(LegacyDragonSkinType.Restore, (uint)session.Character.Uid, 0,
            owner.Book.State.OwnedSkins.Count > 0).ToFrameBytes();
        if (!TryCommitDragonSkinPresentation(context, owner, updated, out updated, out reason))
        { await LogDragonSkinRefusalAsync(context, reason).ConfigureAwait(false); return; }
        try
        {
            if (!await SendCurrentDragonSkinFrameAsync(context, owner, updated, frame, cancellationToken).ConfigureAwait(false)) return;
            await context.Log.WriteLineAsync($"WorldServer restored LegacyDragonSkin {FormatSession(context)} owned={owner.Book.State.OwnedSkins.Count}.").ConfigureAwait(false);
        }
        catch (Exception) { /* Committed presentation is never speculatively restored. */ }
    }

    private static async ValueTask<bool> SendCurrentDragonSkinFrameAsync(WorldPacketHandlerContext context,
        DragonSkinOwner owner, WorldSession expected, byte[] bytes, CancellationToken cancellationToken)
    {
        if (!IsCurrentDragonSkinOwner(context, owner, expected)) return false;
        await context.Connection.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
        return IsCurrentDragonSkinOwner(context, owner, expected);
    }
}
