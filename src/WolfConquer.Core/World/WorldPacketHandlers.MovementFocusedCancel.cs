using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Read-only output DATA. The real movement writer still owns cancellation,
    // native/holder changes and persistence. No source map is read after commit.
    private sealed record LegacyFocusedCancelData(uint Uid, uint SpellId, ushort Facing, uint X, uint Y);

    private static LegacyFocusedCancelData? PrepareLegacyFocusedCancelData(WorldSession original, bool removedFocus)
    {
        if (!removedFocus) return null;
        var attributes = LegacyCharacterAttributeMap.Copy(original.Character.Attributes);
        // Id and ID are spellings of one conceptual key, not fallback sources.
        if (!LegacyCharacterAttributeMap.TryRead(attributes, "FocusClientSpellId", out var rawSpell) ||
            !ulong.TryParse(rawSpell, NumberStyles.Integer, CultureInfo.InvariantCulture, out var spell) ||
            spell == 0 || spell > uint.MaxValue) return null;
        ushort facing = LegacyCharacterAttributeMap.TryRead(attributes, "Angle", out var rawFacing) &&
            ushort.TryParse(rawFacing, out var parsed) ? parsed : (ushort)0;
        return new(CheckedUInt32(original.Character.Uid, nameof(original.Character.Uid)), (uint)spell, facing,
            CheckedUInt32(original.Character.X, nameof(original.Character.X)),
            CheckedUInt32(original.Character.Y, nameof(original.Character.Y)));
    }

    private static async ValueTask SendMovementFocusedCancelIfNeededAsync(WorldPacketHandlerContext context,
        LegacyFocusedCancelData? prepared, CancellationToken cancellationToken)
    {
        if (prepared is null) return;
        var action = new MsgActionPacket
        {
            ObjId = prepared.Uid,
            TargetUid = prepared.SpellId,
            Timestamp = unchecked((uint)Environment.TickCount),
            Type = MsgActionType.CancelFocusedSpell,
            Facing = prepared.Facing,
            PositionX = prepared.X,
            PositionY = prepared.Y,
        };
        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent MsgMovement focused cancel {FormatSession(context)} targetUid={prepared.SpellId}.")
            .ConfigureAwait(false);
    }
}
