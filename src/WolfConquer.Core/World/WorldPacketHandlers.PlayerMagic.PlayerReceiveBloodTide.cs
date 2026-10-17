using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyBloodTideSpellId = 15_500;
    private const int LegacyBloodTideFlag = 0xE5;
    private const uint LegacyBloodTideFineRainFlag = 229;
    private const int LegacyBloodTideRatePercent = 10;
    private const int LegacyBloodTideCooldownSeconds = 70;
    private const string RuntimeLegacyBloodTideRollAttribute = "LegacyBloodTideRoll";
    private const string RuntimeLegacyBloodTideStampUtcAttribute = "LegacyBloodTideStampUtc";
    internal const string RuntimeLegacyBloodTideExpiresAtUtcAttribute = "LegacyBloodTideExpiresAtUtc";
    internal const string RuntimeLegacyBloodTideHitpointsAttribute = "BloodTideHP";

    private static bool TryApplyLegacyPlayerReceiveBloodTide(
        CharacterSnapshot attacker,
        WorldSession? attackerSession,
        WorldSession targetSession,
        WorldDataCatalog? worldData,
        out CharacterSnapshot updatedAttacker,
        out uint bloodTideHitpoints,
        out uint bloodTideDuration,
        out byte bloodTideSpellLevel)
    {
        updatedAttacker = attacker;
        bloodTideHitpoints = 0;
        bloodTideDuration = 0;
        bloodTideSpellLevel = 0;

        if (attackerSession is null ||
            worldData is null ||
            HasLegacyStatusFlag(attacker, LegacyBloodTideFlag) ||
            !IsLegacyTrojanClass(attacker.Class) ||
            !CheckLegacyBloodTideRateRoll(attacker) ||
            !IsLegacyBloodTideCooldownReady(attacker, DateTimeOffset.UtcNow) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                attackerSession.PlayerSpells,
                worldData,
                LegacyBloodTideSpellId,
                out var playerSpell,
                out var bloodTideSpell))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var targetCurrentHitpoints = CheckedUInt32(
            targetSession.Character.MinHitPoints,
            nameof(targetSession.Character.MinHitPoints));
        bloodTideHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            targetSession.Character,
            targetCurrentHitpoints,
            targetSession.PlayerItems,
            targetSession.CombatGear ?? CombatGearSnapshot.Empty,
            worldData,
            targetSession.AlternateEquipmentActive);
        if (attacker.MinHitPoints >= 250_000)
        {
            bloodTideHitpoints = 250_000;
        }

        bloodTideDuration = bloodTideSpell.Duration;
        bloodTideSpellLevel = (byte)Math.Min(playerSpell.Level, byte.MaxValue);
        updatedAttacker = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    WithLegacyStatusFlag(attacker, LegacyBloodTideFlag),
                    RuntimeLegacyBloodTideStampUtcAttribute,
                    now.ToString("O", CultureInfo.InvariantCulture)),
                RuntimeLegacyBloodTideExpiresAtUtcAttribute,
                now.AddSeconds(bloodTideDuration).ToString("O", CultureInfo.InvariantCulture)),
            RuntimeLegacyBloodTideHitpointsAttribute,
            bloodTideHitpoints);
        return true;
    }

    private static bool CheckLegacyBloodTideRateRoll(CharacterSnapshot character)
    {
        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyBloodTideRollAttribute)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < LegacyBloodTideRatePercent;
    }

    private static bool IsLegacyBloodTideCooldownReady(CharacterSnapshot character, DateTimeOffset now)
    {
        if (!character.Attributes.TryGetValue(RuntimeLegacyBloodTideStampUtcAttribute, out var rawStamp) ||
            !DateTimeOffset.TryParse(
                rawStamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var stamp))
        {
            return true;
        }

        return now.ToUniversalTime() >= stamp.AddSeconds(LegacyBloodTideCooldownSeconds);
    }

    private static async ValueTask SendLegacyBloodTideFineRainUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint bloodTideHitpoints,
        uint duration,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyMsgUpdatePacket(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.FineRain,
                    Value: 0,
                    Flag: LegacyBloodTideFineRainFlag,
                    Time: duration,
                    Damage: bloodTideHitpoints,
                    SpellLevel: bloodTideHitpoints),
            ]);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyBloodTideSelfAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte spellLevel,
        CancellationToken cancellationToken)
    {
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = new LegacySpellAnimationPacket(
            uid,
            uid,
            X: 0,
            Y: 0,
            LegacyBloodTideSpellId,
            spellLevel,
            NextSpell: 0,
            LevelHu: 0,
            SpellEffect: 0,
            Targets: []);
        await context.Connection.SendAsync(animation.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyBloodTideEquipmentRefreshAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyShowEquipmentPacket.CreateEquipmentRefresh(
                    session.PlayerItems,
                    session.AlternateEquipmentActive).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
