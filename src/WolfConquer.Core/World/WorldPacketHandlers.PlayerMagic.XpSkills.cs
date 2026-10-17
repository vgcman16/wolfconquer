using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int LegacyCycloneFlag = 0x17;
    private const int LegacyChaintBoltFlag = 0x5C;
    private const int LegacyBladeFlurryFlag = 0x92;
    private const int LegacySuperCycloneFlag = 0x96;
    private const int LegacyDragonCycloneFlag = 0x9F;
    private const int LegacyOmnipotenceFlag = 0xC0;
    private const string RuntimeLegacyCycloneExpiresAtUtcAttribute = "LegacyCycloneExpiresAtUtc";
    private const string RuntimeLegacySupermanExpiresAtUtcAttribute = "LegacySupermanExpiresAtUtc";
    private const string RuntimeLegacyFatalStrikeExpiresAtUtcAttribute = "LegacyFatalStrikeExpiresAtUtc";
    private const string RuntimeLegacyChainBoltExpiresAtUtcAttribute = "LegacyChainBoltExpiresAtUtc";
    private const string RuntimeLegacyBladeFlurryExpiresAtUtcAttribute = "LegacyBladeFlurryExpiresAtUtc";
    private const string RuntimeLegacySuperCycloneExpiresAtUtcAttribute = "LegacySuperCycloneExpiresAtUtc";
    private const string RuntimeLegacyDragonCycloneExpiresAtUtcAttribute = "LegacyDragonCycloneExpiresAtUtc";
    private const string RuntimeLegacyOmnipotenceExpiresAtUtcAttribute = "LegacyOmnipotenceExpiresAtUtc";
    internal const string RuntimeLegacyShurikenVortexExpiresAtUtcAttribute = "LegacyShurikenVortexExpiresAtUtc";
    internal const string RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute =
        "LegacyShurikenVortexNextAttackAtUtc";
    internal const string RuntimeLegacyManiacDanceExpiresAtUtcAttribute = "LegacyManiacDanceExpiresAtUtc";
    internal const string RuntimeLegacyManiacDanceNextAttackAtUtcAttribute = "LegacyManiacDanceNextAttackAtUtc";
    private const string RuntimeLegacyCannonBarrageExpiresAtUtcAttribute = "LegacyCannonBarrageExpiresAtUtc";
    private const string RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute = "LegacyBlackbeardsRageExpiresAtUtc";
    private const string RuntimeLegacyThunderRampageExpiresAtUtcAttribute = "LegacyThunderRampageExpiresAtUtc";
    private static readonly TimeSpan LegacyPirateXpSkillDuration = TimeSpan.FromSeconds(50);
    private static readonly string[] LegacyXpSkillExpiresAtAttributes =
    [
        RuntimeLegacyCycloneExpiresAtUtcAttribute,
        RuntimeLegacySupermanExpiresAtUtcAttribute,
        RuntimeLegacyOblivionExpiresAtUtcAttribute,
        RuntimeLegacyFatalStrikeExpiresAtUtcAttribute,
        RuntimeLegacyChainBoltExpiresAtUtcAttribute,
        RuntimeLegacyBladeFlurryExpiresAtUtcAttribute,
        RuntimeLegacySuperCycloneExpiresAtUtcAttribute,
        RuntimeLegacyDragonCycloneExpiresAtUtcAttribute,
        RuntimeLegacyOmnipotenceExpiresAtUtcAttribute,
        RuntimeLegacyShurikenVortexExpiresAtUtcAttribute,
        RuntimeLegacyShurikenVortexNextAttackAtUtcAttribute,
        RuntimeLegacyManiacDanceExpiresAtUtcAttribute,
        RuntimeLegacyManiacDanceNextAttackAtUtcAttribute,
        RuntimeLegacyCannonBarrageExpiresAtUtcAttribute,
        RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute,
        RuntimeLegacyThunderRampageExpiresAtUtcAttribute,
    ];
    private static readonly int[] LegacyXpSkillFlags =
    [
        LegacyCycloneFlag,
        LegacySupermanFlag,
        LegacyOblivionFlag,
        LegacyFatalStrikeFlag,
        LegacyShurikenVortexFlag,
        LegacyChaintBoltFlag,
        LegacyBlackbeardsRageFlag,
        LegacyCannonBarrageFlag,
        LegacyBladeFlurryFlag,
        LegacySuperCycloneFlag,
        LegacyDragonCycloneFlag,
        LegacyOmnipotenceFlag,
        LegacyManiacDanceFlag,
        LegacyThunderRampageFlag,
    ];

    private static async ValueTask<WorldSession> ApplyLegacyOpenXpSkillAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        int statusFlag,
        string expiresAtAttribute,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        TimeSpan? duration = null)
    {
        var updatedCharacter = WithLegacyOpenXpSkill(
            session.Character,
            statusFlag,
            expiresAtAttribute,
            now,
            duration ?? LegacyPirateXpSkillDuration);
        var updatedSession = session with
        {
            Character = updatedCharacter,
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        var uid = CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid));
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(uid, value: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeXPCircleTickUpdate(uid, count: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context with { Session = updatedSession },
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static CharacterSnapshot WithLegacyOpenXpSkill(
        CharacterSnapshot character,
        int statusFlag,
        string expiresAtAttribute,
        DateTimeOffset now,
        TimeSpan duration)
    {
        var updatedCharacter = WithoutLegacyStatusFlag(character, LegacyRuntimeUpdateTicker.XpListFlag);
        foreach (var xpSkillFlag in LegacyXpSkillFlags)
        {
            if (xpSkillFlag == statusFlag)
            {
                continue;
            }

            updatedCharacter = WithoutLegacyStatusFlag(updatedCharacter, xpSkillFlag);
        }

        foreach (var expiresAtAttributeName in LegacyXpSkillExpiresAtAttributes)
        {
            updatedCharacter = WithoutCharacterAttribute(updatedCharacter, expiresAtAttributeName);
        }

        var expiresAt = now.ToUniversalTime() + duration;
        return WithCharacterAttribute(
            WithCharacterAttribute(
                WithLegacyStatusFlag(updatedCharacter, statusFlag),
                "XPCount",
                0UL),
            expiresAtAttribute,
            expiresAt.ToString("O", CultureInfo.InvariantCulture));
    }
}
