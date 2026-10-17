using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> TryApplyLegacyFamily3319MonsterDeathRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        CancellationToken cancellationToken)
    {
        const string sourceBranch = "MonsterRole.cs Family.ID == 3319";
        var rewardBranch = SelectLegacyFamily3319RewardBranch(session.Character);
        var updatedSession = session;
        var updatedContext = context with { Session = updatedSession };
        var sourceMessage = string.Empty;
        var applied = true;
        ulong? value = null;

        switch (rewardBranch)
        {
            case LegacyFamily3319RewardBranch.GuildDonation:
                {
                    var updatedDonation = checked(
                        ReadAttributeUInt64(
                            updatedSession.Character.Attributes,
                            "GuildDontion",
                            "GuildDonation",
                            "MyDontion") + 1);
                    var updatedCharacter = WithLegacyGuildDonationAttributes(updatedSession.Character, updatedDonation);
                    updatedSession = updatedSession with { Character = updatedCharacter };
                    context.Sessions?.Set(context.Connection, updatedSession);
                    updatedContext = updatedContext with { Session = updatedSession };

                    await SendScalarUpdateAsync(
                            updatedContext,
                            updatedCharacter,
                            LegacyMsgUpdateDataType.MyDontion,
                            updatedDonation,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await SendScalarUpdateAsync(
                            updatedContext,
                            updatedCharacter,
                            LegacyMsgUpdateDataType.DonationPoints,
                            updatedDonation,
                            cancellationToken)
                        .ConfigureAwait(false);

                    value = updatedDonation;
                    sourceMessage = $"{updatedCharacter.Name} He Get 1 Points in His Guild !";
                    break;
                }

            case LegacyFamily3319RewardBranch.Chi:
                {
                    var updatedCharacter = AddLegacyDailySignInChiPoints(updatedSession.Character, 1_000);
                    updatedSession = updatedSession with { Character = updatedCharacter };
                    context.Sessions?.Set(context.Connection, updatedSession);
                    updatedContext = updatedContext with { Session = updatedSession };

                    await context.Connection.SendAsync(
                            ChiInfoPacketProfileSelector.CreateFrame(
                                context.ProtocolProfile,
                                updatedCharacter,
                                LegacyChiInfoAction.Upgrade),
                            cancellationToken)
                        .ConfigureAwait(false);

                    value = checked((ulong)LegacyChiState.FromSnapshot(updatedCharacter).ChiPoints);
                    sourceMessage = $"{updatedCharacter.Name} He Get 100 Chi Points !";
                    break;
                }

            case LegacyFamily3319RewardBranch.Ninja:
                {
                    var currentNinjaPoints = ReadAttributeUInt64(updatedSession.Character.Attributes, "NinjaPoints", "NinjaPoint");
                    var updatedNinjaPoints = checked(currentNinjaPoints + 10);
                    var updatedCharacter = WithCharacterAttribute(
                        updatedSession.Character,
                        "NinjaPoints",
                        updatedNinjaPoints);
                    updatedSession = updatedSession with { Character = updatedCharacter };
                    context.Sessions?.Set(context.Connection, updatedSession);
                    updatedContext = updatedContext with { Session = updatedSession };

                    await LogParityGapAsync(
                            updatedContext,
                            LegacyParityGapDiagnostics.UnsupportedPacket(
                                2_395,
                                packet: "MsgNinjaUser",
                                reason: "source MyNinja.AddPoints sends protobuf MsgNinjaUser Type=2 with updated archive points; modern runtime persists NinjaPoints and sends the source talk message, but the protobuf packet model is not implemented yet",
                                severity: LegacyParityGapSeverity.Info))
                        .ConfigureAwait(false);

                    value = updatedNinjaPoints;
                    sourceMessage = $"{updatedCharacter.Name} He Get 10 Points in His Ninja Archive !";
                    break;
                }

            case LegacyFamily3319RewardBranch.StudyPoints:
                updatedSession = await ApplyLegacyStudyPointAwardAsync(
                        updatedContext,
                        context.Connection,
                        updatedSession,
                        1_000,
                        cancellationToken)
                    .ConfigureAwait(false);
                updatedContext = updatedContext with { Session = updatedSession };
                if (updatedSession.Character.Attributes.TryGetValue("SubProfInfo", out var rawState) &&
                    LegacySubClassHitpointsCalculator.TryParseLegacyState(rawState, out var state))
                {
                    value = state.StudyPoints;
                }

                sourceMessage = $"{updatedSession.Character.Name} He Get 1000 Points in His SupClass !";
                break;

            case LegacyFamily3319RewardBranch.InnerPower1000:
            case LegacyFamily3319RewardBranch.InnerPower500:
                {
                    var potency = rewardBranch == LegacyFamily3319RewardBranch.InnerPower1000 ? 1_000 : 500;
                    applied = await TryApplyLegacyInnerPowerPotencyAwardAsync(
                            updatedContext,
                            context.Connection,
                            updatedSession,
                            potency,
                            cancellationToken)
                        .ConfigureAwait(false);
                    value = checked((ulong)potency);
                    sourceMessage = $"{updatedSession.Character.Name} He Get {potency.ToString(CultureInfo.InvariantCulture)} Points in His innerPower !";
                    break;
                }
        }

        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(sourceMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied legacy family 3319 monster death reward " +
                $"{FormatSession(updatedContext)} monsterUid={monster.Uid} familyId={monsterFamily.Id} map={monster.MapId} " +
                $"reward={FormatLegacyFamily3319RewardBranch(rewardBranch)} applied={applied} value={value?.ToString(CultureInfo.InvariantCulture) ?? "none"} " +
                $"branch=\"{sourceBranch}\".")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static LegacyFamily3319RewardBranch SelectLegacyFamily3319RewardBranch(CharacterSnapshot character)
    {
        if (TryReadLegacyFamily3319RewardBranch(character, out var forcedBranch) &&
            (forcedBranch != LegacyFamily3319RewardBranch.Ninja || IsLegacyNinjaClass(character.Class)))
        {
            return forcedBranch;
        }

        if (CheckLegacyFamily3319Rate(0.001))
        {
            return LegacyFamily3319RewardBranch.GuildDonation;
        }

        if (CheckLegacyFamily3319Rate(1))
        {
            return LegacyFamily3319RewardBranch.Chi;
        }

        if (CheckLegacyFamily3319Rate(0.002) &&
            IsLegacyNinjaClass(character.Class))
        {
            return LegacyFamily3319RewardBranch.Ninja;
        }

        if (CheckLegacyFamily3319Rate(2))
        {
            return LegacyFamily3319RewardBranch.StudyPoints;
        }

        if (CheckLegacyFamily3319Rate(1))
        {
            return LegacyFamily3319RewardBranch.InnerPower1000;
        }

        return LegacyFamily3319RewardBranch.InnerPower500;
    }

    private static bool TryReadLegacyFamily3319RewardBranch(
        CharacterSnapshot character,
        out LegacyFamily3319RewardBranch branch)
    {
        branch = default;
        if (!character.Attributes.TryGetValue(RuntimeLegacyFamily3319RewardBranchAttribute, out var raw))
        {
            return false;
        }

        switch (raw.Trim().ToLowerInvariant())
        {
            case "0":
            case "guild":
            case "guild-donation":
            case "guilddonation":
                branch = LegacyFamily3319RewardBranch.GuildDonation;
                return true;

            case "1":
            case "chi":
            case "chipoints":
            case "chi-points":
                branch = LegacyFamily3319RewardBranch.Chi;
                return true;

            case "2":
            case "ninja":
            case "ninja-points":
            case "ninjaarchive":
            case "ninja-archive":
                branch = LegacyFamily3319RewardBranch.Ninja;
                return true;

            case "3":
            case "study":
            case "study-points":
            case "subclass":
            case "supclass":
                branch = LegacyFamily3319RewardBranch.StudyPoints;
                return true;

            case "4":
            case "inner1000":
            case "inner-power-1000":
            case "innerpower1000":
                branch = LegacyFamily3319RewardBranch.InnerPower1000;
                return true;

            case "5":
            case "inner500":
            case "inner-power-500":
            case "innerpower500":
                branch = LegacyFamily3319RewardBranch.InnerPower500;
                return true;

            default:
                return false;
        }
    }

    private static bool CheckLegacyFamily3319Rate(double ratePercent)
    {
        return Random.Shared.NextDouble() * 100.0 < ratePercent;
    }

    private static CharacterSnapshot WithLegacyGuildDonationAttributes(
        CharacterSnapshot character,
        ulong donation)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        var value = donation.ToString(CultureInfo.InvariantCulture);
        attributes["GuildDontion"] = value;
        attributes["GuildDonation"] = value;
        attributes["MyDontion"] = value;
        return character with { Attributes = attributes };
    }

    private static string FormatLegacyFamily3319RewardBranch(LegacyFamily3319RewardBranch branch)
    {
        return branch switch
        {
            LegacyFamily3319RewardBranch.GuildDonation => "guild-donation",
            LegacyFamily3319RewardBranch.Chi => "chi",
            LegacyFamily3319RewardBranch.Ninja => "ninja",
            LegacyFamily3319RewardBranch.StudyPoints => "study-points",
            LegacyFamily3319RewardBranch.InnerPower1000 => "inner-power-1000",
            LegacyFamily3319RewardBranch.InnerPower500 => "inner-power-500",
            _ => branch.ToString(),
        };
    }

    private enum LegacyFamily3319RewardBranch
    {
        GuildDonation,
        Chi,
        Ninja,
        StudyPoints,
        InnerPower1000,
        InnerPower500,
    }
}
