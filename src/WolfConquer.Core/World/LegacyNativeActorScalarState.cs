using System.Collections.ObjectModel;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Typed runtime projection only. This neither saves a scalar nor issues an owner.
internal static class LegacyNativeActorScalarState
{
    internal readonly record struct Values(byte Reborn, ushort Agility, ushort X, ushort Y, uint Map, uint DynamicID);

    internal static Values RequireCurrent(CharacterSnapshot character)
    {
        var proof = new NativeValueProof(character);
        if (!proof.IsCurrentPlain()) throw Invalid();
        return proof.Value;
    }

    internal static WorldSession ForAdmission(WorldSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var proof = new NativeValueProof(session.Character, allowAbsentDynamicId: true);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in proof.CapturedFields)
            if (!string.Equals(pair.Key, "DynamicID", StringComparison.OrdinalIgnoreCase)) fields.Add(pair.Key, pair.Value);
        // The source runtime's absent dynamic-map identity is zero. This key
        // belongs to the admitted runtime map; no Character file is rewritten.
        fields.Add("DynamicID", proof.DynamicIdText ?? "0");
        var final = session with
        {
            Character = session.Character with { Attributes = new ReadOnlyDictionary<string, string>(fields) },
        };
        if (!proof.IsCurrentPlain()) throw Invalid();
        return final;
    }

    internal sealed class NativeValueProof
    {
        private readonly LegacyNativeAttributeMapProof attributes;
        internal Values Value { get; }
        internal string? DynamicIdText { get; }
        internal IReadOnlyDictionary<string, string> CapturedFields => attributes.Fields;

        internal NativeValueProof(CharacterSnapshot character, bool allowAbsentDynamicId = false)
        {
            if (character is null || character.Reborn is < byte.MinValue or > byte.MaxValue ||
                character.Agility is < ushort.MinValue or > ushort.MaxValue ||
                character.X is < ushort.MinValue or > ushort.MaxValue || character.Y is < ushort.MinValue or > ushort.MaxValue ||
                character.MapId is < uint.MinValue or > uint.MaxValue) throw Invalid();
            attributes = new(character.Attributes);
            var present = attributes.Fields.TryGetValue("DynamicID", out var text);
            uint dynamicId = 0;
            if (!present && !allowAbsentDynamicId || present &&
                !uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out dynamicId)) throw Invalid();
            Value = new((byte)character.Reborn, (ushort)character.Agility, (ushort)character.X,
                (ushort)character.Y, (uint)character.MapId, dynamicId);
            DynamicIdText = text;
        }

        internal bool IsCurrentPlain() => attributes.IsCurrentPlain();
    }

    private static InvalidOperationException Invalid() => new("ordinaryActorScalarStateInvalidOrAmbiguous");
}
