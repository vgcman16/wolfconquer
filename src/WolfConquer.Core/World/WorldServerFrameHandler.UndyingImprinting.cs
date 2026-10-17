using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const ushort LegacyThunderStrikerUndyingImprintingSpellId = 15_820;
    private const uint LegacyUndyingImprintingStatusFlag = 0xEA;
    private const uint LegacyThunderStrikerUndyingImprintingLoginSeed = 60;
    private const uint LegacyThunderStrikerUndyingImprintingBaseLimit = 60;
    private const uint LegacyThunderStrikerUndyingImprintingFightingWillLimit = 120;
    private const uint LegacyThunderStrikerUndyingImprintingStaminaGain = 20;
    private const string RuntimeThunderStrikerUndyingImprintingAttribute = "ThunderStrikerUndyingImprinting";

    private async ValueTask SendLegacyThunderStrikerUndyingImprintingStartupAsync(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        PlayerSpellSnapshot playerSpells,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyThunderStrikerClass(snapshot.Class) ||
            !playerSpells.ContainsSpell(LegacyThunderStrikerUndyingImprintingSpellId))
        {
            return;
        }

        var uid = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        await connection.SendAsync(
                CreateLegacyThunderStrikerUndyingImprintingUpdate(uid, damage: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        if (_sessions.TryGet(connection, out var session) && session is not null)
        {
            var attributes = new Dictionary<string, string>(
                session.Character.Attributes,
                StringComparer.OrdinalIgnoreCase);
            attributes[RuntimeThunderStrikerUndyingImprintingAttribute] =
                LegacyThunderStrikerUndyingImprintingLoginSeed.ToString(CultureInfo.InvariantCulture);
            AddLegacyUndyingImprintingStatusFlag(attributes);

            _sessions.Set(
                connection,
                session with
                {
                    Character = session.Character with { Attributes = attributes },
                });
        }

        await connection.SendAsync(
                CreateLegacyThunderStrikerUndyingImprintingUpdate(
                        uid,
                        charge: LegacyThunderStrikerUndyingImprintingLoginSeed)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask SendLegacyThunderStrikerUndyingImprintingRuntimeTickAsync(
        ConquerTcpConnection connection,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        CancellationToken cancellationToken)
    {
        if (!_sessions.TryGet(connection, out var session) ||
            session is null ||
            !IsLegacyThunderStrikerClass(session.Character.Class) ||
            !playerSpells.ContainsSpell(LegacyThunderStrikerUndyingImprintingSpellId))
        {
            return;
        }

        var charge = ReadLegacyUndyingImprintingCharge(session.Character);
        var limit = HasLegacyFightingWillRune(playerItems, _worldData)
            ? LegacyThunderStrikerUndyingImprintingFightingWillLimit
            : LegacyThunderStrikerUndyingImprintingBaseLimit;

        var updatedCharge = charge;
        var restoreStamina = false;
        if (charge < limit)
        {
            updatedCharge = charge + 1;
            restoreStamina = updatedCharge % LegacyThunderStrikerUndyingImprintingLoginSeed == 0 &&
                updatedCharge > 0;
        }
        else if (charge > limit)
        {
            updatedCharge = limit;
        }

        if (updatedCharge == charge)
        {
            return;
        }

        var attributes = new Dictionary<string, string>(
            session.Character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes[RuntimeThunderStrikerUndyingImprintingAttribute] =
            updatedCharge.ToString(CultureInfo.InvariantCulture);
        if (updatedCharge >= LegacyThunderStrikerUndyingImprintingLoginSeed)
        {
            AddLegacyUndyingImprintingStatusFlag(attributes);
        }
        else
        {
            RemoveLegacyStatusFlag(attributes, (int)LegacyUndyingImprintingStatusFlag);
        }

        ulong? staminaUpdate = null;
        if (restoreStamina)
        {
            var maximumStamina = HasLegacyUndyingImprintingHeavenBlessing(attributes) ? 150UL : 100UL;
            var currentStamina = ReadLegacyUndyingImprintingStamina(attributes);
            staminaUpdate = Math.Min(currentStamina + LegacyThunderStrikerUndyingImprintingStaminaGain, maximumStamina);
            attributes["Stamina"] = staminaUpdate.Value.ToString(CultureInfo.InvariantCulture);
        }

        var updatedSession = session with
        {
            Character = session.Character with { Attributes = attributes },
        };
        _sessions.Set(connection, updatedSession);

        var uid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        if (updatedCharge % LegacyThunderStrikerUndyingImprintingLoginSeed == 0)
        {
            await connection.SendAsync(
                    CreateLegacyThunderStrikerUndyingImprintingUpdate(uid, charge: updatedCharge).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (staminaUpdate is { } stamina)
        {
            await connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, stamina).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static LegacyMsgUpdatePacket CreateLegacyThunderStrikerUndyingImprintingUpdate(
        uint uid,
        uint charge = LegacyThunderStrikerUndyingImprintingLoginSeed,
        uint? damage = null)
    {
        return new LegacyMsgUpdatePacket(
            uid,
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.UndyingImprinting,
                    Value: 0,
                    Flag: LegacyUndyingImprintingStatusFlag,
                    Time: LegacyThunderStrikerUndyingImprintingLoginSeed - (charge % LegacyThunderStrikerUndyingImprintingLoginSeed),
                    Damage: damage ?? charge / LegacyThunderStrikerUndyingImprintingLoginSeed,
                    SpellLevel: 0),
            ]);
    }

    private static bool IsLegacyThunderStrikerClass(long classCode)
    {
        return classCode is >= 9_000 and <= 9_057;
    }

    private static uint ReadLegacyUndyingImprintingCharge(CharacterSnapshot character)
    {
        return character.Attributes.TryGetValue(RuntimeThunderStrikerUndyingImprintingAttribute, out var raw) &&
            uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var charge)
            ? charge
            : 0;
    }

    private static ulong ReadLegacyUndyingImprintingStamina(IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.TryGetValue("Stamina", out var rawStamina) &&
            ulong.TryParse(rawStamina, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stamina))
        {
            return stamina;
        }

        return HasLegacyUndyingImprintingHeavenBlessing(attributes) ? 150UL : 100UL;
    }

    private static bool HasLegacyUndyingImprintingHeavenBlessing(IReadOnlyDictionary<string, string> attributes)
    {
        foreach (var key in new[] { "HeavenBlessing", "HeavensBlessing" })
        {
            if (attributes.TryGetValue(key, out var raw) &&
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
                value > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLegacyFightingWillRune(PlayerItemSnapshot playerItems, WorldDataCatalog? worldData)
    {
        if (worldData is null)
        {
            return false;
        }

        foreach (var item in playerItems.RuneItems)
        {
            if (item.ItemId > int.MaxValue ||
                !worldData.ItemsById.TryGetValue((int)item.ItemId, out var itemTypes))
            {
                continue;
            }

            foreach (var itemType in itemTypes)
            {
                if (itemType.Name.Equals("FightingWill", StringComparison.OrdinalIgnoreCase) ||
                    itemType.Name.StartsWith("FightingWill(", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void AddLegacyUndyingImprintingStatusFlag(Dictionary<string, string> attributes)
    {
        LegacyStatusFlagDeathRegistration.PrepareAdd(attributes, (int)LegacyUndyingImprintingStatusFlag);
        var key = attributes.ContainsKey("BitVector") && !attributes.ContainsKey("StatusFlags")
            ? "BitVector"
            : "StatusFlags";
        var raw = attributes.TryGetValue(key, out var existingRaw) ? existingRaw ?? "" : "";
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wordIndex = (int)LegacyUndyingImprintingStatusFlag / 32;
        var words = new ulong[Math.Max(
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength,
            Math.Max(parts.Length, wordIndex + 1))];
        for (var index = 0; index < Math.Min(parts.Length, words.Length); index++)
        {
            ulong.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out words[index]);
        }

        words[wordIndex] |= 1UL << ((int)LegacyUndyingImprintingStatusFlag % 32);
        attributes[key] = string.Join('/', words) + "/";
    }
}
