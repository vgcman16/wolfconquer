using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal static bool TryReadCapriceOutputStamina(CharacterSnapshot character, out ulong stamina)
    {
        stamina = 0;
        try
        {
            var captured = new FullAccountStringMap(character.Attributes);
            var witness = captured.Open();
            var stable = character with { Attributes = captured.Copy };
            return captured.Copy.ContainsKey("Stamina") && TryReadLegacyRuntimeStamina(stable, out stamina) && captured.Matches(witness);
        }
        catch (InvalidOperationException) { return false; }
    }

    // Closed projection data, never saved/Store authority or a callback bag.
    internal sealed class LegacyCapriceCharacterFields
    {
        private readonly ulong before, after;
        private readonly string? stamp;
        internal LegacyCapriceCharacterFields(ulong before, ulong after, string? stamp)
        { this.before = before; this.after = after; this.stamp = stamp; }
        internal ulong AfterStamina => after;
        internal bool Matches(CharacterSnapshot expected, CharacterSnapshot proposed) =>
            TryReadLegacyRuntimeStamina(expected, out var old) && old == before &&
            TryReadLegacyRuntimeStamina(proposed, out var next) && next == after &&
            (stamp is null || proposed.Attributes.TryGetValue(RuntimeLegacyCapriceStampUtcAttribute, out var value) && value == stamp);
        internal void AddTo(Dictionary<string, string> values)
        {
            values["Stamina"] = after.ToString(CultureInfo.InvariantCulture);
            if (stamp is not null) values[RuntimeLegacyCapriceStampUtcAttribute] = stamp;
        }
        internal bool MatchesImage(CharacterSnapshot saved) => saved.Attributes.TryGetValue("Stamina", out var raw) &&
            ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value == after &&
            (stamp is null || saved.Attributes.TryGetValue(RuntimeLegacyCapriceStampUtcAttribute, out var savedStamp) && savedStamp == stamp);
    }

    internal sealed class LegacyCapricePairIntent
    {
        internal LegacyCapriceCharacterFields Attacker { get; }
        internal LegacyCapriceCharacterFields Target { get; }
        private LegacyCapricePairIntent(ulong beforeAttacker, ulong afterAttacker, ulong beforeTarget, ulong afterTarget, string stamp)
        {
            Attacker = new(beforeAttacker, afterAttacker, stamp);
            Target = new(beforeTarget, afterTarget, null);
        }
        internal static bool TryCreate(WorldSession attacker, WorldSession target,
            WorldSession proposedAttacker, WorldSession proposedTarget, out LegacyCapricePairIntent? intent)
        {
            intent = null;
            if (!TryGetEquippedLegacyCapriceLevel(attacker.PlayerItems, out _) ||
                !TryReadLegacyRuntimeStamina(attacker.Character, out var beforeAttacker) ||
                !TryReadLegacyRuntimeStamina(target.Character, out var beforeTarget) || beforeTarget < 20 ||
                beforeAttacker > ulong.MaxValue - 20 ||
                !TryReadLegacyRuntimeStamina(proposedAttacker.Character, out var afterAttacker) || afterAttacker != beforeAttacker + 20 ||
                !TryReadLegacyRuntimeStamina(proposedTarget.Character, out var afterTarget) || afterTarget != beforeTarget - 20 ||
                !proposedAttacker.Character.Attributes.TryGetValue(RuntimeLegacyCapriceStampUtcAttribute, out var stamp) ||
                !DateTimeOffset.TryParseExact(stamp, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return false;
            intent = new(beforeAttacker, afterAttacker, beforeTarget, afterTarget, stamp); return true;
        }
        internal bool Matches(WorldSession attacker, WorldSession target, WorldSession proposedAttacker, WorldSession proposedTarget) =>
            Attacker.Matches(attacker.Character, proposedAttacker.Character) && Target.Matches(target.Character, proposedTarget.Character);
    }
}
