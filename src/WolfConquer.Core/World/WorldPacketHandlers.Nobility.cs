using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly object LegacyPaidNobilityPersistenceLock = new();

    [PacketHandler(WorldPacketOpcodes.LegacyNobilityIcon)]
    private static async ValueTask HandleLegacyNobilityAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyNobilityRequestPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyNobility packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNobility " +
                $"{FormatSession(context)} action={packet.Action} value={packet.Value} donationType={packet.DonationType}.")
            .ConfigureAwait(false);

        if (packet.Action == LegacyNobilityAction.RankListen)
        {
            var page = packet.Value > ushort.MaxValue ? ushort.MaxValue : (ushort)packet.Value;
            var response = LegacyNobilityRankListPacket.FromEntries(
                page,
                LegacyNobilityRanking.CreateRankListEntries(context.Session?.UsersRoot, context.Sessions));
            await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer sent LegacyNobility rank list {FormatSession(context)} page={response.Page} " +
                    $"count={response.TotalCount} entries={response.Entries.Count}.")
                .ConfigureAwait(false);
        }
        else if (packet.Action == LegacyNobilityAction.NobilityInformation)
        {
            var thresholds = LegacyNobilityRanking.CreateThresholds(context.Session?.UsersRoot, context.Sessions);
            await context.Connection.SendAsync(
                    new LegacyNobilityInformationPacket(
                            thresholds.DukeDonation,
                            thresholds.PrinceDonation,
                            thresholds.KingDonation)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    $"WorldServer sent LegacyNobility information {FormatSession(context)}.")
                .ConfigureAwait(false);
        }
        else if (packet.Action == LegacyNobilityAction.Donate)
        {
            await HandleLegacyNobilityDonationAsync(context, packet, cancellationToken).ConfigureAwait(false);
        }
        else if (packet.Action == LegacyNobilityAction.Icon)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored source-backed LegacyNobility icon action " +
                    $"{FormatSession(context)} value={packet.Value} donationType={packet.DonationType}; " +
                    "legacy MsgNobility.Icon handler is a no-op.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP unmodeled LegacyNobility " +
                    $"{FormatSession(context)} action={packet.Action} value={packet.Value} " +
                    $"donationType={packet.DonationType}; donation/rank mutation is not modeled.")
                .ConfigureAwait(false);
        }
    }

    private static bool TryPersistLegacyCharacterAndPaidNobilityAttributes(
        string? usersRoot,
        long accountId,
        IReadOnlyDictionary<string, string> characterValues,
        IReadOnlyDictionary<string, string> paidNobilityValues,
        out string path,
        out string reason)
    {
        path = string.Empty;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            reason = "usersRootMissing";
            return false;
        }

        path = Path.Combine(usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        lock (LegacyPaidNobilityPersistenceLock)
        {
            if (!File.Exists(path))
            {
                reason = "snapshotMissing";
                return false;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, LegacyCharacterSnapshotEncoding);
            }
            catch (IOException)
            {
                reason = "snapshotReadFailed";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                reason = "snapshotReadDenied";
                return false;
            }
            catch (System.Security.SecurityException)
            {
                reason = "snapshotReadDenied";
                return false;
            }

            if (!TryApplyLegacySectionAttributeLines(
                    lines,
                    "Character",
                    characterValues,
                    createIfMissing: false,
                    out var updatedLines))
            {
                reason = "characterSectionMissing";
                return false;
            }

            if (!TryApplyLegacySectionAttributeLines(
                    updatedLines,
                    "PaidNobility",
                    paidNobilityValues,
                    createIfMissing: true,
                    out updatedLines))
            {
                reason = "paidNobilitySectionMissing";
                return false;
            }

            var transactionId = Guid.NewGuid().ToString("N");
            var tempPath = path + ".paid-nobility-" + transactionId + ".tmp";
            var transactionBackupPath = path + ".paid-nobility-" + transactionId + ".bak";
            var replacingSnapshot = false;
            var preserveBackup = false;

            try
            {
                File.WriteAllLines(tempPath, updatedLines, LegacyCharacterSnapshotEncoding);
                replacingSnapshot = true;
                File.Replace(
                    tempPath,
                    path,
                    transactionBackupPath,
                    ignoreMetadataErrors: true);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                reason = replacingSnapshot ? "snapshotReplaceDenied" : "snapshotWriteDenied";
                if (replacingSnapshot &&
                    !TryRestoreLegacyPaidNobilitySnapshot(path, transactionBackupPath))
                {
                    reason = "snapshotRollbackFailed";
                    preserveBackup = true;
                }

                return false;
            }
            catch (Exception exception) when (
                exception is IOException or
                System.Security.SecurityException or
                ArgumentException or
                NotSupportedException)
            {
                reason = replacingSnapshot ? "snapshotReplaceFailed" : "snapshotWriteFailed";
                if (replacingSnapshot &&
                    !TryRestoreLegacyPaidNobilitySnapshot(path, transactionBackupPath))
                {
                    reason = "snapshotRollbackFailed";
                    preserveBackup = true;
                }

                return false;
            }
            finally
            {
                TryDeleteLegacyPaidNobilityPersistenceArtifact(tempPath);
                if (!preserveBackup)
                {
                    TryDeleteLegacyPaidNobilityPersistenceArtifact(transactionBackupPath);
                }
            }
        }
    }

    private static bool TryRestoreLegacyPaidNobilitySnapshot(
        string path,
        string transactionBackupPath)
    {
        if (!File.Exists(transactionBackupPath))
        {
            return true;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Replace(
                    transactionBackupPath,
                    path,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(transactionBackupPath, path);
            }

            return true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            System.Security.SecurityException or
            ArgumentException or
            NotSupportedException)
        {
            return false;
        }
    }

    private static void TryDeleteLegacyPaidNobilityPersistenceArtifact(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.Security.SecurityException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }


    private static async ValueTask<bool> TryPurchaseLegacyAccountManagerPaidNobilityAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        byte rank,
        ulong cost,
        CancellationToken cancellationToken)
    {
        var donatePoints = ReadAttributeUInt64(session.Character.Attributes, "DonatePoints");
        if (donatePoints < cost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.AccountManager",
                    [
                        string.Concat(
                            LegacyAccountManagerInsufficientDonatePointsText,
                            " You need ",
                            cost.ToString(CultureInfo.InvariantCulture),
                            " donation points."),
                    ],
                    [new LegacyNpcDialogOption("I~see.", LegacyNpcClosedOption)],
                    LegacyAccountManagerShopAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager paid nobility " +
                    $"{FormatSession(context)} source=NpcHandler.AccountManager option={optionId} " +
                    $"cost={cost} donatePoints={donatePoints}.")
                .ConfigureAwait(false);
            return true;
        }

        var previousDonation = LegacyNobilityRanking.ReadRegularDonation(session.Character);
        var updatedDonatePoints = donatePoints - cost;
        var position = LegacyPaidNobilityPosition(rank);
        var periodTime = DateTime.Now.AddDays(LegacyAccountManagerPaidNobilityDays);
        var periodBinary = periodTime.ToBinary();
        var updatedCharacter = WithLegacyPaidNobilityPurchase(
            session.Character,
            updatedDonatePoints,
            previousDonation,
            rank,
            position,
            periodBinary);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DonatePoints"] = updatedDonatePoints.ToString(CultureInfo.InvariantCulture),
            ["DonationNobility"] = "0",
            ["LastNobilityDonation"] = "0",
            ["NobilityRank"] = rank.ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = position.ToString(CultureInfo.InvariantCulture),
        };
        var paidNobilityValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = updatedCharacter.Uid.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = previousDonation.ToString(CultureInfo.InvariantCulture),
            ["PaidRank"] = rank.ToString(CultureInfo.InvariantCulture),
            ["PeriodTime"] = periodBinary.ToString(CultureInfo.InvariantCulture),
            ["IsActive"] = bool.TrueString,
            ["Rank"] = LegacyNobilityRanking.RankName(rank),
            ["Position"] = position.ToString(CultureInfo.InvariantCulture),
        };

        if (TryPersistLegacyCharacterAndPaidNobilityAttributes(
                session.UsersRoot,
                session.AccountId,
                characterValues,
                paidNobilityValues,
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc AccountManager paid nobility " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"path={Path.GetFileName(path)} keys=DonatePoints,DonationNobility,LastNobilityDonation," +
                    "NobilityRank,NobilityPosition,PaidNobility.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc AccountManager paid nobility persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                    $"reason={reason}.")
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc AccountManager paid nobility " +
                $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                $"option={optionId} cost={cost} rank={rank} " +
                $"rankName={LegacyNobilityRanking.RankName(rank)} days={LegacyAccountManagerPaidNobilityDays} " +
                $"previousDonation={previousDonation} donatePoints={updatedDonatePoints} " +
                $"periodTime={periodBinary}.")
            .ConfigureAwait(false);

        await context.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer disconnected LegacyNpc AccountManager paid nobility " +
                $"{FormatSession(context with { Session = updatedSession })} source=NpcHandler.AccountManager " +
                $"option={optionId}.")
            .ConfigureAwait(false);
        return true;
    }
}
