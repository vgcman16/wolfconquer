using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyPairMovementCharacterProjection
{
    private const int MaximumBytes = 64 * 1024 * 1024;

    internal static bool TryProject(byte[] before, LegacyPairMovementOperation operation, uint participantUid,
        out byte[] after, out string reason)
    {
        after = []; reason = "pairMovementCharacterInvalid";
        try
        {
            ArgumentNullException.ThrowIfNull(operation);
            operation.Validate();
            if (!operation.Contains(participantUid)) return false;
            return TryProjectCoordinates(before, participantUid, operation.MapId, operation.FromX, operation.FromY,
                operation.ToX, operation.ToY, out after, out reason);
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or OverflowException)
        {
            reason += ":" + error.GetType().Name; return false;
        }
    }

    // Pure lossless image projection. The actual ordinary/pair movement owner
    // must separately validate geometry, authorization and durable commitment.
    internal static bool TryProjectCoordinates(byte[] before, uint participantUid, uint map,
        ushort fromX, ushort fromY, ushort toX, ushort toY, out byte[] after, out string reason)
    {
        after = []; reason = "movementCharacterInvalid";
        try
        {
            ArgumentNullException.ThrowIfNull(before);
            if (before.Length is 0 or > MaximumBytes || participantUid == 0 || map == 0) return false;
            var captured = before.ToArray();
            var text = CharacterSnapshotFileEncoding.Decode(captured, out var encoding);
            if (!Encode(text, encoding).AsSpan().SequenceEqual(captured)) return false;
            var fields = ReadFields(text);
            ValidateOptionalDynamicId(fields);
            var saved = CharacterSnapshotParser.ParseText(text);
            if (saved.Uid != participantUid || saved.MapId != map || saved.X != fromX || saved.Y != fromY) return false;
            if (!fields.TryGetValue("X", out var x) || !fields.TryGetValue("Y", out var y)) return false;
            var replacements = new[] { (Field: x, Value: toX, Changed: saved.X != toX),
                    (Field: y, Value: toY, Changed: saved.Y != toY) }
                .Where(value => value.Changed)
                .OrderByDescending(value => value.Field.Start);
            foreach (var replacement in replacements)
                text = text[..replacement.Field.Start] + replacement.Value.ToString(CultureInfo.InvariantCulture) +
                    text[(replacement.Field.Start + replacement.Field.Length)..];
            var result = Encode(text, encoding);
            if (result.Length > MaximumBytes) return false;
            var projected = CharacterSnapshotParser.ParseText(text);
            if (projected.Uid != participantUid || projected.MapId != map || projected.X != toX || projected.Y != toY) return false;
            after = result; reason = string.Empty; return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or InvalidDataException or
            KeyNotFoundException or OverflowException)
        {
            reason += ":" + error.GetType().Name; return false;
        }
    }

    private static void ValidateOptionalDynamicId(Dictionary<string, Field> fields)
    {
        if (!fields.TryGetValue("DynamicID", out var field)) return;
        if (!uint.TryParse(field.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            throw new InvalidDataException("pairMovementDynamicIdInvalid");
    }

    private static byte[] Encode(string text, Encoding encoding)
    {
        var preamble = encoding.GetPreamble(); var body = encoding.GetBytes(text);
        var bytes = new byte[checked(preamble.Length + body.Length)];
        preamble.CopyTo(bytes, 0); body.CopyTo(bytes, preamble.Length); return bytes;
    }
}
