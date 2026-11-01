using WolfConquer.Core.Data;
using System.Collections.ObjectModel;
using System.Globalization;

namespace WolfConquer.Core.World;

// Immutable calculation only; this value grants no Store or persistence authority.
internal readonly record struct LegacyOwnedAttributeSetProjection(
    ushort Strength, ushort Agility, ushort Vitality, ushort Spirit, ushort Remaining)
{
    internal static bool TryCreate(CharacterSnapshot character, LegacyAttributeSetPacket packet,
        out LegacyOwnedAttributeSetProjection result)
    {
        result = default;
        if (character is null || character.Strength is < 0 or > ushort.MaxValue ||
            character.Agility is < 0 or > ushort.MaxValue || character.Vitality is < 0 or > ushort.MaxValue ||
            character.Spirit is < 0 or > ushort.MaxValue || character.AttributePoints is < 0 or > ushort.MaxValue)
            return false;
        ulong total = (ulong)packet.Strength + packet.Agility + packet.Vitality + packet.Spirit;
        if (character.AttributePoints == 0 || total > (ulong)character.AttributePoints ||
            packet.Strength > ushort.MaxValue - character.Strength ||
            packet.Agility > ushort.MaxValue - character.Agility ||
            packet.Vitality > ushort.MaxValue - character.Vitality ||
            packet.Spirit > ushort.MaxValue - character.Spirit) return false;
        result = new((ushort)(character.Strength + packet.Strength), (ushort)(character.Agility + packet.Agility),
            (ushort)(character.Vitality + packet.Vitality), (ushort)(character.Spirit + packet.Spirit),
            (ushort)(character.AttributePoints - (long)total));
        return true;
    }
}

// Frozen data proposal; it is not a publication capability.
internal sealed class LegacyOwnedAttributeSetProposal
{
    private static readonly string[] Keys = ["Strength", "Agility", "Vitaliti", "Spirit", "Atributes"];
    private readonly LegacyNativeAttributeMapProof witness;
    internal CharacterSnapshot Before { get; }
    internal CharacterSnapshot After { get; }
    internal LegacyOwnedAttributeSetProjection Values { get; }
    internal LegacyAttributeSetPacket Packet { get; }
    private LegacyOwnedAttributeSetProposal(CharacterSnapshot before, CharacterSnapshot after,
        LegacyNativeAttributeMapProof witness, LegacyOwnedAttributeSetProjection values, LegacyAttributeSetPacket packet)
    { Before = before; After = after; this.witness = witness; Values = values; Packet = packet; }
    internal bool IsCurrentPlain() => witness.IsCurrentPlain();

    internal static bool TryCreate(CharacterSnapshot before, LegacyAttributeSetPacket packet, out LegacyOwnedAttributeSetProposal? proposal)
    {
        proposal = null;
        if (!LegacyOwnedAttributeSetProjection.TryCreate(before, packet, out var values)) return false;
        LegacyNativeAttributeMapProof proof;
        try { proof = LegacyNativeAttributeMapProof.CaptureRaw(before.Attributes); }
        catch (InvalidOperationException) { return false; }
        string[] replacement = [values.Strength.ToString(CultureInfo.InvariantCulture), values.Agility.ToString(CultureInfo.InvariantCulture),
            values.Vitality.ToString(CultureInfo.InvariantCulture), values.Spirit.ToString(CultureInfo.InvariantCulture),
            values.Remaining.ToString(CultureInfo.InvariantCulture)];
        var seen = new bool[Keys.Length];
        var copy = new Dictionary<string, string>(proof.CapturedComparer);
        foreach (var pair in proof.Fields)
        {
            int index = Array.FindIndex(Keys, key => key.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && seen[index]) return false;
            if (index >= 0) seen[index] = true;
            copy.Add(pair.Key, index < 0 ? pair.Value : replacement[index]);
        }
        for (int index = 0; index < Keys.Length; index++)
            if (!seen[index]) copy.Add(Keys[index], replacement[index]);
        if (!proof.IsCurrentPlain()) return false;
        var after = before with { Strength = values.Strength, Agility = values.Agility, Vitality = values.Vitality,
            Spirit = values.Spirit, AttributePoints = values.Remaining, Attributes = new ReadOnlyDictionary<string, string>(copy) };
        proposal = new(before, after, proof, values, packet); return true;
    }
}
