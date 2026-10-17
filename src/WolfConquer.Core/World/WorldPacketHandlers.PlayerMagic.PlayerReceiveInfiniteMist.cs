using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyInfiniteMistSpellId = 16_380;
    private const int LegacyInfiniteMistFlag = 0x11A;
    private const string RuntimeLegacyInfiniteMistRollAttribute = "LegacyInfiniteMistRoll";
    private const string RuntimeLegacySkyVeilInfiniteMistRollAttribute = "LegacySkyVeilInfiniteMistRoll";
    private const string RuntimeLegacySkyVeilRollAttribute = "LegacySkyVeilRoll";
    internal const string RuntimeLegacyInfiniteMistExpiresAtUtcAttribute = "LegacyInfiniteMistExpiresAtUtc";

    private readonly record struct LegacyPlayerReceiveInfiniteMistResult(
        bool Applied,
        bool StatusChanged,
        uint DefenderUid,
        byte SpellLevel,
        byte UseSpellSoul,
        uint Duration,
        uint Damage)
    {
        public bool IsActive => Applied;
    }

    private static LegacyPlayerReceiveInfiniteMistResult TryApplyLegacyPlayerReceiveInfiniteMist(
        WorldSession attackerSession,
        WorldSession defenderSession,
        WorldDataCatalog? worldData,
        out CharacterSnapshot updatedAttacker)
    {
        updatedAttacker = attackerSession.Character;
        if (worldData is null ||
            defenderSession.Character.MinHitPoints < 1 ||
            !IsLegacyNinjaClass(defenderSession.Character.Class) ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                defenderSession.PlayerSpells,
                worldData,
                LegacyInfiniteMistSpellId,
                out var playerSpell,
                out var infiniteMistSpell) ||
            !TryReadLegacyMagicUInt32Field(
                worldData,
                LegacyInfiniteMistSpellId,
                playerSpell.Level,
                LegacyMagicRateField,
                out var rate) ||
            !CheckLegacyReceiveInfiniteMistRateRoll(defenderSession.Character, rate) ||
            IsLegacyInfiniteMistSuppressedBySkyVeil(attackerSession, worldData))
        {
            return default;
        }

        var hadFlag = HasLegacyStatusFlag(updatedAttacker, LegacyInfiniteMistFlag);
        if (!hadFlag)
        {
            updatedAttacker = WithCharacterAttribute(
                WithLegacyStatusFlag(updatedAttacker, LegacyInfiniteMistFlag),
                RuntimeLegacyInfiniteMistExpiresAtUtcAttribute,
                DateTimeOffset.UtcNow
                    .ToUniversalTime()
                    .AddSeconds(infiniteMistSpell.Duration)
                    .ToString("O", CultureInfo.InvariantCulture));
        }

        return new LegacyPlayerReceiveInfiniteMistResult(
            Applied: true,
            StatusChanged: !hadFlag,
            DefenderUid: CheckedUInt32(defenderSession.Character.Uid, nameof(defenderSession.Character.Uid)),
            SpellLevel: (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            infiniteMistSpell.Duration,
            infiniteMistSpell.Damage);
    }

    private static bool IsLegacyInfiniteMistSuppressedBySkyVeil(
        WorldSession attackerSession,
        WorldDataCatalog worldData)
    {
        if (!TryGetLegacyEquippedRuneLevel(
                attackerSession.PlayerItems,
                worldData,
                "Sky Veil",
                out var skyVeilLevel))
        {
            return false;
        }

        var rate = skyVeilLevel switch
        {
            1 => 20,
            2 => 30,
            3 => 40,
            4 => 50,
            5 => 60,
            6 => 70,
            7 => 80,
            8 => 90,
            9 => 100,
            _ => 0,
        };
        return CheckLegacyReceiveInfiniteMistRateRoll(
            attackerSession.Character,
            checked((uint)rate),
            RuntimeLegacySkyVeilInfiniteMistRollAttribute,
            RuntimeLegacySkyVeilRollAttribute);
    }

    private static bool CheckLegacyReceiveInfiniteMistRateRoll(
        CharacterSnapshot character,
        uint rate,
        params string[] forcedRollAttributes)
    {
        if (rate == 0)
        {
            return false;
        }

        if (rate >= 100)
        {
            return true;
        }

        var attributes = forcedRollAttributes.Length == 0
            ? [RuntimeLegacyInfiniteMistRollAttribute]
            : forcedRollAttributes;
        var roll = TryReadUInt32Attribute(character.Attributes, out var forcedRoll, attributes)
            ? forcedRoll % 100
            : checked((uint)Random.Shared.Next(0, 100));
        return roll < rate;
    }

    private static async ValueTask SendLegacyPlayerReceiveInfiniteMistAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        if (!result.InfiniteMistResult.IsActive)
        {
            return;
        }

        if (result.InfiniteMistResult.StatusChanged &&
            result.UpdatedAttackerSession is { } updatedAttackerSession)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    context,
                    context.Connection,
                    updatedAttackerSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var defenderSession = result.UpdatedSession;
        var defenderContext = context with
        {
            Connection = result.ActiveSession.Connection,
            Session = defenderSession,
        };
        var animation = new LegacySpellAnimationPacket(
            result.InfiniteMistResult.DefenderUid,
            result.InfiniteMistResult.DefenderUid,
            X: 0,
            Y: 0,
            LegacyInfiniteMistSpellId,
            result.InfiniteMistResult.SpellLevel,
            NextSpell: 0,
            LevelHu: result.InfiniteMistResult.UseSpellSoul,
            SpellEffect: 0,
            Targets: [new LegacySpellAnimationTarget(
                result.InfiniteMistResult.DefenderUid,
                Damage: 0,
                Hit: true,
                MsgAttackEffect.None)]);

        await SendPlayerSpellAnimationToVisibleAsync(
                defenderContext,
                defenderSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy InfiniteMist player-hit counter " +
                $"{FormatSession(defenderContext)} attackerUid={attacker.Character.Uid} " +
                $"statusFlag={LegacyInfiniteMistFlag} duration={result.InfiniteMistResult.Duration}; " +
                "source=ReceiveAttack.Player.Execute -> AttachStatus case 16380.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyKineticSparkPlayerReceiveInfiniteMistArchiveSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession attacker,
        LegacyPlayerMagicDamageResult result,
        CancellationToken cancellationToken)
    {
        if (!result.InfiniteMistResult.IsActive)
        {
            return;
        }

        var uid = CheckedUInt32(attacker.Character.Uid, nameof(attacker.Character.Uid));
        var packet = new LegacyMsgUpdatePacket(
            uid,
            [
                new LegacyMsgUpdateEntry(
                    LegacyMsgUpdateDataType.ArchiveSkill,
                    Value: 0,
                    Flag: LegacyInfiniteMistFlag,
                    Time: result.InfiniteMistResult.Duration,
                    Damage: result.InfiniteMistResult.Damage)
            ]);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy KineticSpark player receive InfiniteMist archive update " +
                $"{FormatSession(context with { Session = attacker })} defenderUid={result.InfiniteMistResult.DefenderUid} " +
                $"statusFlag={LegacyInfiniteMistFlag} duration={result.InfiniteMistResult.Duration} " +
                $"damage={result.InfiniteMistResult.Damage}; source=ReceiveAttack.Player.Execute InfiniteMist.")
            .ConfigureAwait(false);
    }
}
