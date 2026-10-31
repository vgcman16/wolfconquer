using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyStaminaRegenerationRules
{
    internal readonly record struct Result(ushort Stamina, bool Fan, bool Effect);

    internal static bool TryPrepare(WorldSession source, LegacyNativeStatusFlagsBigVector32.StaminaFlags flags,
        bool joinedFan, byte monkBonus, DateTime entryNow, DateTime gateNow, DateTime staminaStamp,
        DateTime fanStamp, out Result result)
    {
        result = default;
        ushort current = LegacyStaminaState.RequireCurrent(source.Character);
        var words = LegacyWindWalkerStatusProjection.Read(source.Character);
        bool Has(int flag) => flags.Has(flag) || (words[flag / 32] & (1UL << (flag % 32))) != 0;
        if (source.Character.MinHitPoints <= 0 || Has(27) || Has(219)) return false;
        // These existing positive modifiers need their complete actual grant/
        // clock/query owners. Never silently publish a base-only substitute.
        if (Has(214) || Has(240) || OmnipotenceApplicable(flags, words)) return false;
        if (source.Character.Class is >= 1000 and <= 1051 && current < 60 && source.PlayerSpells.ContainsSpell(14_190)) return false;
        int maximum = checked((ReadBlessing(source.Character) > 0 ? 150 : 100) + monkBonus);
        if (maximum > byte.MaxValue) throw new InvalidDataException("staminaMaximumByteOverflow");
        if (current >= maximum) return false;
        bool sit = ReadSit(source.Character);
        if (gateNow < staminaStamp.AddSeconds(sit ? 1 : 3)) return false;
        int gain = sit ? 15 : 5;
        bool fan = flags.Fan.Present && joinedFan && entryNow > fanStamp.AddSeconds(5);
        if (flags.Fan.Present && !joinedFan) return false;
        if (fan) gain = checked(gain + 10);
        result = new(checked((ushort)Math.Min(maximum, checked(current + gain))), fan,
            fan && checked(current + gain) < maximum);
        return true;
    }

    private static bool OmnipotenceApplicable(LegacyNativeStatusFlagsBigVector32.StaminaFlags flags, ulong[] words)
    {
        // Preserve actual OnXPSkill first-present priority. AutoHunt is absent
        // from the source return list and cannot invent a20-point bonus.
        LegacyNativeMsgUpdate.Flags[] priority = [LegacyNativeMsgUpdate.Flags.Cyclone, LegacyNativeMsgUpdate.Flags.Superman,
            LegacyNativeMsgUpdate.Flags.Oblivion, LegacyNativeMsgUpdate.Flags.FatalStrike, LegacyNativeMsgUpdate.Flags.ShurikenVortex,
            LegacyNativeMsgUpdate.Flags.ChaintBolt, LegacyNativeMsgUpdate.Flags.BlackbeardsRage, LegacyNativeMsgUpdate.Flags.CannonBarrage,
            LegacyNativeMsgUpdate.Flags.BladeFlurry, LegacyNativeMsgUpdate.Flags.SuperCyclone, LegacyNativeMsgUpdate.Flags.DragonCyclone,
            LegacyNativeMsgUpdate.Flags.Omnipotence];
        foreach (var flag in priority)
        {
            int id = (int)flag;
            if (flag == LegacyNativeMsgUpdate.Flags.Omnipotence)
                return flags.Has(id) || (words[id / 32] & (1UL << (id % 32))) != 0;
            // A stored earlier bit is not proof that it can mask an actual
            // physical Omnipotence modifier. Only the current real graph can.
            if (flags.Has(id)) return false;
        }
        return false;
    }
    private static int ReadBlessing(CharacterSnapshot character)
    {
        int? value = null;
        foreach (var pair in character.Attributes)
        {
            if (!pair.Key.Equals("HeavenBlessing", StringComparison.OrdinalIgnoreCase) &&
                !pair.Key.Equals("HeavensBlessing", StringComparison.OrdinalIgnoreCase)) continue;
            if (!int.TryParse(pair.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var next) || value is { } prior && next != prior)
                throw new InvalidDataException("staminaBlessingAliasesInvalid");
            value = next;
        }
        return value ?? 0;
    }
    private static bool ReadSit(CharacterSnapshot character)
    {
        if (!character.Attributes.TryGetValue("Action", out var raw)) return false;
        if (raw is null) throw new InvalidDataException("staminaActionInvalid");
        if (raw.Equals("Sit", StringComparison.OrdinalIgnoreCase)) return true;
        if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return value == 250;
        if (Enum.TryParse<LegacyNativePlayerMapFlags.ConquerAction>(raw, true, out var action))
            return action == LegacyNativePlayerMapFlags.ConquerAction.Sit;
        throw new InvalidDataException("staminaActionInvalid");
    }
}
