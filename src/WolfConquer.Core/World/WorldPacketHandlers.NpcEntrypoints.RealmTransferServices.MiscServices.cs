using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyRealmTransferServicesGreenSnakeNpcId = 30_115;
    private const uint LegacyRealmTransferServicesVoteManagerNpcId = 52_300;
    private const uint LegacyRealmTransferServicesFootballNpcId = 52_001;
    private const uint LegacyRealmTransferServicesNorbertNpcId = 421;
    private const uint LegacyRealmTransferServicesDrYinYangNpcId = 15_805;

    private const uint LegacyRealmTransferServicesBombItemId = 721_261;
    private const uint LegacyRealmTransferServicesSaltpeterItemId = 721_262;
    private const uint LegacyRealmTransferServicesSulphurItemId = 721_263;
    private const int LegacyRealmTransferServicesNorbertSaltpeterCount = 5;
    private const int LegacyRealmTransferServicesNorbertSulphurCount = 1;
    private const long LegacyRealmTransferServicesDrYinYangConquerPointCost = 1_075;
    private const long LegacyRealmTransferServicesVoteConquerPointReward = 3_000_000;
    private const ulong LegacyRealmTransferServicesVoteNobilityDonationReward = 2_000_000_000;
    private const long LegacyRealmTransferServicesConquerPointInventoryCap = 2_000_000_000;

    private const ushort LegacyRealmTransferServicesGreenSnakeAvatarId = 110;
    private const ushort LegacyRealmTransferServicesVoteAvatarId = 3;
    private const ushort LegacyRealmTransferServicesVoteRewardAvatarId = 63;
    private const ushort LegacyRealmTransferServicesNorbertAvatarId = 67;
    private const ushort LegacyRealmTransferServicesDrYinYangAvatarId = 218;

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesGreenSnakeRootOptions =
    [
        new("Be~off,~or~I~will~kill~you!", 1),
        new("I~am~gone,~bye!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesVoteRootOptions =
    [
        new("Vote~for~us.", 40),
        new("Claim~rewards.", 41),
        new("I~will~think~about~it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesVoteRewardOptions =
    [
        new("1~point~for~3M~CPs.", 42),
        new("1~point~for~5M~donation.", 48),
        new("10~points~for~Anima~13.", 43),
        new("2~points~for~Super~Relic.", 44),
        new("3~points~for~1000~Ninja~Points.", 45),
        new("2~points~for~Yellow~Rune.", 46),
        new("4~points~for~VIP~6~(12~hours).", 47),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesFootballRootOptions =
    [
        new("Sign~up.", 1),
        new("Special~rewards?", 2),
        new("I~will~think~about~it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesFootballRewardOptions =
    [
        new("Anima~9~(30~points).", 3),
        new("Anima~10~(60~points).", 4),
        new("I~will~think~about~it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesNorbertMaterialOptions =
    [
        new("Yes.", 10),
        new("Just~passing~by.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesNorbertSuccessOptions =
    [
        new("I see. Thanks for the remind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesNorbertRootOptions =
    [
        new("What~is~the~Bomb?", 1),
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyRealmTransferServicesDrYinYangRootOptions =
    [
        new("Please~change~my~gender,~now.", 1),
        new("What~else~should~I~know?", 2),
        new("Not~interested.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesGreenSnakeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.GreenSnake";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["What beautiful scenery. I feel lucky to meet you here, so my hunger can be solved. Come here!"],
                        LegacyRealmTransferServicesGreenSnakeRootOptions,
                        LegacyRealmTransferServicesGreenSnakeAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["Wait, I am joking. Spare me and I will lead you to a marvelous place with treasure everywhere, if you can break through the Snake Array."],
                        [new LegacyNpcDialogOption("What~is~the~Snake~Array?", 2)],
                        LegacyRealmTransferServicesGreenSnakeAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["The Snake Array is a battle array formed by 17 islands. Our boss watches the last one. Beat the boss and the treasure is yours."],
                        [new LegacyNpcDialogOption("Show~me~the~way.", 3)],
                        LegacyRealmTransferServicesGreenSnakeAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["I do not question your ability, but greed may cost your life. I have seen that happen often."],
                        [
                            new LegacyNpcDialogOption("I~fear~nothing.", 4),
                            new LegacyNpcDialogOption("I~changed~my~mind.", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesGreenSnakeAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        LegacyRealmTransferServicesGreenSnakeNpcId,
                        optionId,
                        new LegacyNpcTeleportDestination(1_063, 444, 355, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesVoteManagerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.VoteManager";
        const string sourceLines = "25769-25898";

        switch (optionId)
        {
            case 0:
                if (session.Character.Level < 100)
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["You must reach Level 100 before you can vote."],
                            LegacyRealmTransferServicesCloseOptions,
                            LegacyRealmTransferServicesVoteAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                var votePoints = ReadAttributeUInt64(session.Character.Attributes, "VotePoints");
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Welcome to the vote manager. You are eligible to vote once every 12 hours.",
                            $"Each successful vote grants 1 vote point. You currently have {votePoints.ToString(CultureInfo.InvariantCulture)} vote point(s).",
                        ],
                        LegacyRealmTransferServicesVoteRootOptions,
                        LegacyRealmTransferServicesVoteAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 40:
                await StartLegacyRealmTransferServicesVoteAsync(
                        context,
                        source,
                        sourceLines,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 41:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["Use vote points to claim server rewards. Keep voting to help attract more players."],
                        LegacyRealmTransferServicesVoteRewardOptions,
                        LegacyRealmTransferServicesVoteRewardAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 42:
                await ApplyLegacyRealmTransferServicesVoteConquerPointRewardAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 43:
            case 44:
            case 45:
            case 46:
                await ApplyLegacyRealmTransferServicesVoteItemRewardAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 47:
                await ApplyLegacyRealmTransferServicesVoteVipRewardAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 48:
                await ApplyLegacyRealmTransferServicesVoteNobilityRewardAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
        }

        return false;
    }

    private static async ValueTask ApplyLegacyRealmTransferServicesVoteItemRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        CancellationToken cancellationToken)
    {
        const string sourceLines = "25832-25868";
        var (cost, rewardItemId) = optionId switch
        {
            43 => (10UL, 4_200_013U),
            44 => (2UL, 3_321_806U),
            45 => (3UL, 729_463U),
            46 => (2UL, 3_307_212U),
            _ => throw new ArgumentOutOfRangeException(nameof(optionId)),
        };

        var votePoints = ReadAttributeUInt64(session.Character.Attributes, "VotePoints");
        if (votePoints < cost)
        {
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager item reward " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        var updatedVotePoints = checked(votePoints - cost);
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            "VotePoints",
            updatedVotePoints);
        var hasInventorySpace = HasLegacyInventorySpace(session.PlayerItems, 1);
        if (!hasInventorySpace)
        {
            await LogLegacyRealmTransferServicesVoteItemSafetyFenceAsync(
                    context, source, sourceLines, optionId, rewardItemId, "inventory-full")
                .ConfigureAwait(false);
            return;
        }
        PlayerItemSnapshotItem? grantedItem = null;

        if (hasInventorySpace)
        {
            if (!TryReadLegacyItemType(context.WorldData, rewardItemId, out var rewardItemType))
            {
                await LogLegacyRealmTransferServicesVoteItemSafetyFenceAsync(
                        context,
                        source,
                        sourceLines,
                        optionId,
                        rewardItemId,
                        "itemtype-missing")
                    .ConfigureAwait(false);
                return;
            }

            if (rewardItemType.Id != rewardItemId || rewardItemType.LegacyDurability > ushort.MaxValue)
            {
                await LogLegacyRealmTransferServicesVoteItemSafetyFenceAsync(
                        context,
                        source,
                        sourceLines,
                        optionId,
                        rewardItemId,
                        "itemtype-invalid")
                    .ConfigureAwait(false);
                return;
            }

            try
            {
                grantedItem = CreatePurchasedItems(
                        session.PlayerItems,
                        rewardItemType,
                        count: 1,
                        isBound: false)[0]
                    .WithColor(checked((uint)Random.Shared.Next(3, 9)));
            }
            catch (OverflowException)
            {
                await LogLegacyRealmTransferServicesVoteItemSafetyFenceAsync(
                        context,
                        source,
                        sourceLines,
                        optionId,
                        rewardItemId,
                        "item-uid-exhausted")
                    .ConfigureAwait(false);
                return;
            }
        }

        var updatedItems = grantedItem is null
            ? session.PlayerItems
            : session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems
                    .Concat([grantedItem])
                    .OrderBy(item => item.Uid)
                    .ToArray(),
            };
        var updatedSession = session with
        {
            Character = updatedCharacter,
            PlayerItems = updatedItems,
        };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VotePoints"] = updatedVotePoints.ToString(CultureInfo.InvariantCulture),
        };

        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: grantedItem is not null,
                out var persistenceReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager item reward persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={rewardItemId} reason={persistenceReason}; " +
                    "no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        if (grantedItem is not null)
        {
            await context.Connection.SendAsync(
                    LegacyGameItemPacket
                        .FromSnapshotItem(grantedItem, mode: LegacyGameItemPacket.AddItemMode)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices VoteManager item reward " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                $"votePoints={votePoints}->{updatedVotePoints} rewardItemId={rewardItemId} " +
                $"inventoryFull={!hasInventorySpace} packetOrder=" +
                (grantedItem is null ? "none" : "GameItemAdd") + ".")
            .ConfigureAwait(false);
    }

    private static Task LogLegacyRealmTransferServicesVoteItemSafetyFenceAsync(
        WorldPacketHandlerContext context,
        string source,
        string sourceLines,
        byte optionId,
        uint rewardItemId,
        string reason)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc RealmTransferServices VoteManager item reward " +
            $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
            $"rewardItemId={rewardItemId} reason={reason} " +
            "safetyDifference=metadata-and-uid-are-preflighted-before-the-source-debit-add-sequence; " +
            "no mutation or packets were committed.");
    }

    private static async ValueTask ApplyLegacyRealmTransferServicesVoteVipRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        CancellationToken cancellationToken)
    {
        const string sourceLines = "25870-25887";
        const ulong cost = 4;
        const ulong vipLevel = 6;
        var votePoints = ReadAttributeUInt64(session.Character.Attributes, "VotePoints");
        if (votePoints < cost)
        {
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager VIP reward " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        var now = DateTime.Now;
        var currentExpiry = ReadLegacyRewardPointsVipExpiry(session.Character);
        DateTime updatedExpiry;
        try
        {
            updatedExpiry = (currentExpiry > now ? currentExpiry : now).AddHours(12);
        }
        catch (ArgumentOutOfRangeException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager VIP reward " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=vip-expiry-out-of-range; no mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        var updatedVotePoints = checked(votePoints - cost);
        var vipTime = updatedExpiry.ToBinary().ToString(CultureInfo.InvariantCulture);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(session.Character, "VotePoints", updatedVotePoints),
                "VipLevel",
                vipLevel),
            "VipTime",
            vipTime);
        var updatedSession = session with { Character = updatedCharacter };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VotePoints"] = updatedVotePoints.ToString(CultureInfo.InvariantCulture),
            ["VipLevel"] = "6",
            ["VipTime"] = vipTime,
        };

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryPersistLegacyRewardPointsMutation(
                context,
                updatedSession,
                characterValues,
                persistItems: false,
                out var persistenceReason))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager VIP reward persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"reason={persistenceReason}; no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        var vipUpdate = LegacyMsgUpdatePacket.CreateLegacyVipLevelStartupUpdate(updatedCharacter).ToFrameBytes();

        await context.Connection.SendAsync(vipUpdate, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(vipUpdate, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyVipStatusPacket.FromSnapshot(updatedCharacter).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(".....").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices VoteManager VIP reward " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                $"votePoints={votePoints}->{updatedVotePoints} vipLevel={vipLevel} vipTime={vipTime} " +
                "packetOrder=VipLevel,VipLevel,VipStatusFullVip,NpcMessageBox.")
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacyRealmTransferServicesVoteConquerPointRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        CancellationToken cancellationToken)
    {
        const string sourceLines = "25889-25895";
        var votePoints = ReadAttributeUInt64(session.Character.Attributes, "VotePoints");
        if (votePoints < 1)
        {
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager CP reward " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager CP reward " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=users-root-missing; no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        var rewardedConquerPoints = checked(
            session.Character.ConquerPoints + LegacyRealmTransferServicesVoteConquerPointReward);
        var overflowConquerPoints = Math.Max(
            0,
            rewardedConquerPoints - LegacyRealmTransferServicesConquerPointInventoryCap);
        var updatedConquerPoints = rewardedConquerPoints - overflowConquerPoints;
        var updatedBankConquerPoints = checked(
            session.Character.BankConquerPoints + overflowConquerPoints);
        var updatedVotePoints = checked(votePoints - 1);
        var updatedCharacter = session.Character with
        {
            ConquerPoints = updatedConquerPoints,
            BankConquerPoints = updatedBankConquerPoints,
        };
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "ConquerPoints",
            checked((ulong)updatedConquerPoints));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            "BankCps",
            checked((ulong)updatedBankConquerPoints));
        updatedCharacter = WithCharacterAttribute(updatedCharacter, "VotePoints", updatedVotePoints);
        var updatedSession = session with { Character = updatedCharacter };

        bool persisted;
        string persistencePath;
        string persistenceReason;
        try
        {
            persisted = CharacterSnapshotScalarPersistence.TryPersist(
                session.UsersRoot,
                updatedCharacter,
                out persistencePath,
                out persistenceReason);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            persisted = false;
            persistencePath = string.Empty;
            persistenceReason = ex.GetType().Name;
        }

        if (!persisted)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager CP reward persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"reason={persistenceReason} path={persistencePath}; " +
                    "no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        if (overflowConquerPoints > 0)
        {
            await context.Connection.SendAsync(
                    LegacyNpcReplyPacket
                        .ForMessageBox(
                            $"{overflowConquerPoints.ToString(CultureInfo.InvariantCulture)} " +
                            "Your ConquerPoints[CPS] has Transfare To Your Bank Thanks.")
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTopLeft("....").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices VoteManager CP reward " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                $"votePoints={votePoints}->{updatedVotePoints} " +
                $"conquerPoints={session.Character.ConquerPoints}->{updatedConquerPoints} " +
                $"bankCps={session.Character.BankConquerPoints}->{updatedBankConquerPoints} " +
                $"overflow={overflowConquerPoints} packetOrder=" +
                (overflowConquerPoints > 0
                    ? "NpcMessageBox,ConquerPoints,TopLeftSystem2005"
                    : "ConquerPoints,TopLeftSystem2005") + ".")
            .ConfigureAwait(false);
    }

    private static async ValueTask ApplyLegacyRealmTransferServicesVoteNobilityRewardAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        CancellationToken cancellationToken)
    {
        const string sourceLines = "25846-25854";
        var votePoints = ReadAttributeUInt64(session.Character.Attributes, "VotePoints");
        if (votePoints < 1)
        {
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager nobility reward " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        var character = session.Character;
        var regularNobilityCharacter = character with
        {
            PaidNobility = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
        var donorState = LegacyNobilityRanking.ResolveForSnapshot(
            regularNobilityCharacter,
            session.UsersRoot,
            sessions);
        var rankingCandidates = LegacyNobilityRanking
            .CreateRankListEntries(session.UsersRoot, sessions)
            .Select(static entry => new LegacyNobilityState(
                entry.Uid,
                entry.Name,
                entry.Gender,
                entry.Mesh,
                entry.Donation,
                entry.Rank,
                entry.Position))
            .ToArray();
        var result = LegacyNobilityDonationRuntime.ApplyExternalDonation(
            donorState,
            LegacyRealmTransferServicesVoteNobilityDonationReward,
            rankingCandidates);
        if (!result.Applied)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RealmTransferServices VoteManager nobility reward " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"failure={result.Failure}; no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        var updatedVotePoints = votePoints - 1;
        var updatedCharacter = WithCharacterAttribute(character, "VotePoints", updatedVotePoints);
        updatedCharacter = WithLegacyNobilityState(updatedCharacter, result.DonorAfter);
        var updatedSession = session with { Character = updatedCharacter };
        var characterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VotePoints"] = updatedVotePoints.ToString(CultureInfo.InvariantCulture),
            ["DonationNobility"] = result.DonorAfter.Donation.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = result.DonorAfter.Donation.ToString(CultureInfo.InvariantCulture),
            ["NobilityRank"] = result.DonorAfter.Rank.ToString(CultureInfo.InvariantCulture),
            ["NobilityPosition"] = result.DonorAfter.Position.ToString(CultureInfo.InvariantCulture),
        };
        var paidNobilityValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Identifier"] = updatedCharacter.Uid.ToString(CultureInfo.InvariantCulture),
            ["LastNobilityDonation"] = result.DonorAfter.Donation.ToString(CultureInfo.InvariantCulture),
            ["PaidRank"] = result.DonorAfter.Rank.ToString(CultureInfo.InvariantCulture),
            ["Rank"] = LegacyNobilityRanking.RankName(result.DonorAfter.Rank),
            ["Position"] = result.DonorAfter.Position.ToString(CultureInfo.InvariantCulture),
        };

        bool persisted;
        string persistencePath;
        string persistenceReason;
        try
        {
            persisted = TryPersistLegacyCharacterAndPaidNobilityAttributes(
                session.UsersRoot,
                session.AccountId,
                characterValues,
                paidNobilityValues,
                out persistencePath,
                out persistenceReason);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            persisted = false;
            persistencePath = string.Empty;
            persistenceReason = exception.GetType().Name;
        }

        if (!persisted)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices VoteManager nobility reward persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"reason={persistenceReason} path={persistencePath}; " +
                    "no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };
        await context.Connection.SendAsync(
                new LegacyNobilityIconPacket(
                        result.DonorAfter.Uid,
                        result.DonorAfter.Donation,
                        result.DonorAfter.Rank,
                        result.DonorAfter.Position)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNobilityChangedIconsAsync(updatedContext, result, cancellationToken)
            .ConfigureAwait(false);

        var promotionBroadcast = ShouldBroadcastLegacyNobilityPromotion(result);
        if (promotionBroadcast)
        {
            await BroadcastLegacyNobilityPromotionAsync(
                    updatedContext,
                    result.DonorAfter,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices VoteManager nobility reward " +
                $"{FormatSession(updatedContext)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                $"votePoints={votePoints}->{updatedVotePoints} " +
                $"donation={result.DonorBefore.Donation}->{result.DonorAfter.Donation} " +
                $"rank={result.DonorBefore.Rank}->{result.DonorAfter.Rank} " +
                $"position={result.DonorBefore.Position}->{result.DonorAfter.Position} " +
                $"changedIcons={result.ChangedIcons.Count} promotionBroadcast={promotionBroadcast} " +
                "packetOrder=NobilityIcon,ChangedNobilityIcons,PromotionBroadcast.")
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesFootballAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.Football";
        const string sourceLines = "25901-26015";

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "Football Tournament starts daily at 16:30 and awards CPs and Anima.",
                            "There are two teams. Players fight for the ball with line spells, then score by carrying it into the enemy square.",
                            "The first team to reach 5 goals wins, or the team with the most goals after 10 minutes wins.",
                        ],
                        LegacyRealmTransferServicesFootballRootOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyRealmTransferServicesGapAsync(
                        context,
                        LegacyRealmTransferServicesFootballNpcId,
                        optionId,
                        source,
                        sourceLines,
                        "validate line spells and equipment, then join the scheduled Football tournament",
                        "Football tournament entry is unavailable here until global tournament schedule and entry validation are modeled safely.",
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                var points = ReadAttributeUInt64(session.Character.Attributes, "MyFootBallPoints", "FootballPoints");
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [$"Football points can be exchanged for Anima rewards. You currently have {points.ToString(CultureInfo.InvariantCulture)} point(s)."],
                        LegacyRealmTransferServicesFootballRewardOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
            case 4:
                await ApplyLegacyRealmTransferServicesFootballRewardAsync(
                        context,
                        session,
                        optionId,
                        source,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesNorbertAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.Norbert";
        const string sourceLines = "26313-26369";

        switch (optionId)
        {
            case 0:
                if (LegacyRealmTransferServicesHasUnboundNorbertMaterial(
                        session,
                        LegacyRealmTransferServicesSaltpeterItemId,
                        LegacyRealmTransferServicesNorbertSaltpeterCount) &&
                    LegacyRealmTransferServicesHasUnboundNorbertMaterial(
                        session,
                        LegacyRealmTransferServicesSulphurItemId,
                        LegacyRealmTransferServicesNorbertSulphurCount))
                {
                    await SendLegacyNpcDialogAsync(
                            context,
                            source,
                            ["Hi, you have the materials I need. Did you come for the Bomb?"],
                            LegacyRealmTransferServicesNorbertMaterialOptions,
                            LegacyRealmTransferServicesNorbertAvatarId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    return true;
                }

                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["Five pieces of Saltpeter and one piece of Sulphur are required to make the Bomb. I charge a fee of 30,000 silvers."],
                        LegacyRealmTransferServicesNorbertRootOptions,
                        LegacyRealmTransferServicesNorbertAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "The Bomb is used to blow down the guild gate.",
                            "I saw many people die attacking the gate in a guild war, so I invented a powerful bomb to destroy it.",
                        ],
                        [
                            new LegacyNpcDialogOption("Can~you~make~one~for~me?", 2),
                            new LegacyNpcDialogOption("Oh.~I~see.", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesNorbertAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["No problem, but the Bomb is powerful and dangerous. Whoever uses it to destroy a guild gate will be killed by it too."],
                        [
                            new LegacyNpcDialogOption("Thanks.~I~need~it~badly.", 3),
                            new LegacyNpcDialogOption("Then~forget~it.", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesNorbertAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["Collect 5 Saltpeter and 1 Sulphur. You will also need to pay 30,000 silvers as a fee."],
                        [
                            new LegacyNpcDialogOption("Where~do~I~get~the~materials?", 4),
                            new LegacyNpcDialogOption("I~will~give~up.", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesNorbertAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 4:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        ["You can get Saltpeter from my friend Old Quarrier in Ape City, and Sulphur from nearby Bandits."],
                        [new LegacyNpcDialogOption("Okay,~I~will~be~back~soon.", LegacyNpcClosedOption)],
                        LegacyRealmTransferServicesNorbertAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 10:
                if (!LegacyRealmTransferServicesHasUnboundNorbertMaterial(
                        session,
                        LegacyRealmTransferServicesSaltpeterItemId,
                        LegacyRealmTransferServicesNorbertSaltpeterCount) ||
                    !LegacyRealmTransferServicesHasUnboundNorbertMaterial(
                        session,
                        LegacyRealmTransferServicesSulphurItemId,
                        LegacyRealmTransferServicesNorbertSulphurCount))
                {
                    await context.Log.WriteLineAsync(
                            "WorldServer skipped LegacyNpc RealmTransferServices Norbert Bomb craft " +
                            $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                            "reason=unbound-materials-missing; source case 10 sends no dialog and no inventory mutation.")
                        .ConfigureAwait(false);
                    return true;
                }

                await CraftLegacyRealmTransferServicesNorbertBombAsync(
                        context,
                        session,
                        source,
                        sourceLines,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask CraftLegacyRealmTransferServicesNorbertBombAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string source,
        string sourceLines,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var playerItemsRoot = context.PlayerItemsRoot;
        if (string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            await SendLegacyRealmTransferServicesGapAsync(
                    context,
                    LegacyRealmTransferServicesNorbertNpcId,
                    optionId,
                    source,
                    sourceLines,
                    "remove 5 Saltpeter and 1 Sulphur, then grant Bomb item 721261",
                    "Bomb crafting is unavailable here until inventory removal and item grants are modeled safely.",
                    LegacyRealmTransferServicesNorbertAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Norbert Bomb craft " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no item persistence or inventory mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyItemType(context.WorldData, LegacyRealmTransferServicesBombItemId, out var bombItemType))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Norbert Bomb craft " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesBombItemId} reason=itemtype-missing; " +
                    "no materials were consumed.")
                .ConfigureAwait(false);
            return;
        }

        if (bombItemType.Id != LegacyRealmTransferServicesBombItemId ||
            bombItemType.LegacyDurability > ushort.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Norbert Bomb craft " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesBombItemId} reason=itemtype-invalid; " +
                    "no materials were consumed.")
                .ConfigureAwait(false);
            return;
        }

        var consumptionFrames = new List<byte[]>(
            LegacyRealmTransferServicesNorbertSaltpeterCount +
            LegacyRealmTransferServicesNorbertSulphurCount);
        PlayerItemSnapshotItem grantedItem;
        try
        {
            grantedItem = CreatePurchasedItems(
                session.PlayerItems,
                bombItemType,
                count: 1,
                isBound: false)[0];
        }
        catch (OverflowException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Norbert Bomb craft " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesBombItemId} reason=item-uid-exhausted; " +
                    "no materials were consumed.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItems = session.PlayerItems;
        for (var index = 0; index < LegacyRealmTransferServicesNorbertSaltpeterCount; index++)
        {
            var saltpeter = updatedItems.InventoryItems
                .Where(item => item.ItemId == LegacyRealmTransferServicesSaltpeterItemId && item.Bound == 0)
                .OrderBy(item => item.Uid)
                .FirstOrDefault();
            if (saltpeter is null)
            {
                await LogLegacyRealmTransferServicesNorbertMaterialDriftAsync(
                        context,
                        source,
                        sourceLines,
                        optionId,
                        LegacyRealmTransferServicesSaltpeterItemId)
                    .ConfigureAwait(false);
                return;
            }

            updatedItems = ConsumeSingleLegacyInventoryItem(updatedItems, saltpeter, out var consumptionFrame);
            consumptionFrames.Add(consumptionFrame);
        }

        for (var index = 0; index < LegacyRealmTransferServicesNorbertSulphurCount; index++)
        {
            var sulphur = updatedItems.InventoryItems
                .Where(item => item.ItemId == LegacyRealmTransferServicesSulphurItemId && item.Bound == 0)
                .OrderBy(item => item.Uid)
                .FirstOrDefault();
            if (sulphur is null)
            {
                await LogLegacyRealmTransferServicesNorbertMaterialDriftAsync(
                        context,
                        source,
                        sourceLines,
                        optionId,
                        LegacyRealmTransferServicesSulphurItemId)
                    .ConfigureAwait(false);
                return;
            }

            updatedItems = ConsumeSingleLegacyInventoryItem(updatedItems, sulphur, out var consumptionFrame);
            consumptionFrames.Add(consumptionFrame);
        }

        if (!HasLegacyInventorySpace(updatedItems, 1))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Norbert Bomb craft " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesBombItemId} reason=inventory-full; " +
                    "no materials were consumed.")
                .ConfigureAwait(false);
            return;
        }

        updatedItems = updatedItems with
        {
            InventoryItems = updatedItems.InventoryItems
                .Concat([grantedItem])
                .OrderBy(item => item.Uid)
                .ToArray(),
        };
        var updatedSession = session with { PlayerItems = updatedItems };

        try
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(
                updatedSession.Character.Uid,
                updatedItems);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices Norbert Bomb craft persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"rewardItemId={LegacyRealmTransferServicesBombItemId} reason={ex.GetType().Name}; " +
                    "no session mutation was committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        foreach (var consumptionFrame in consumptionFrames)
        {
            await context.Connection.SendAsync(consumptionFrame, cancellationToken).ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyGameItemPacket
                    .FromSnapshotItem(grantedItem, mode: LegacyGameItemPacket.AddItemMode)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                updatedContext,
                source,
                ["You can take the Bomb away. Remember that you`ll be killed once you use it to destroy the guild gate!"],
                LegacyRealmTransferServicesNorbertSuccessOptions,
                LegacyRealmTransferServicesNorbertAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices Norbert Bomb craft " +
                $"{FormatSession(updatedContext)} source={source} sourceLines=26352-26360 optionId={optionId} " +
                $"saltpeterItemId={LegacyRealmTransferServicesSaltpeterItemId} " +
                $"saltpeterCount={LegacyRealmTransferServicesNorbertSaltpeterCount} " +
                $"sulphurItemId={LegacyRealmTransferServicesSulphurItemId} " +
                $"sulphurCount={LegacyRealmTransferServicesNorbertSulphurCount} " +
                $"rewardItemId={LegacyRealmTransferServicesBombItemId} bound=false silverDebited=0.")
            .ConfigureAwait(false);
    }

    private static bool LegacyRealmTransferServicesHasUnboundNorbertMaterial(
        WorldSession session,
        uint itemId,
        int requiredCount)
    {
        var count = 0;
        foreach (var item in session.PlayerItems.InventoryItems)
        {
            if (item.ItemId != itemId || item.Bound != 0)
            {
                continue;
            }

            count += item.StackSize == 0 ? 1 : item.StackSize;
            if (count >= requiredCount)
            {
                return true;
            }
        }

        return false;
    }

    private static Task LogLegacyRealmTransferServicesNorbertMaterialDriftAsync(
        WorldPacketHandlerContext context,
        string source,
        string sourceLines,
        byte optionId,
        uint itemId)
    {
        return context.Log.WriteLineAsync(
            "PARITY-GAP LegacyNpc RealmTransferServices Norbert Bomb craft " +
            $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
            $"itemId={itemId} reason=material-count-drift; no session mutation was committed.");
    }

    private static async ValueTask<bool> TryHandleLegacyRealmTransferServicesDrYinYangAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        const string source = "NpcHandler.DrYinYang";
        const string sourceLines = "26372-26490";

        switch (optionId)
        {
            case 0:
            case 3:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "If you want to change your gender for a new life, I can help.",
                            "Before the magic of gender reassignment works, you need to pay 1,075 CPs and understand the rules and procedure.",
                        ],
                        LegacyRealmTransferServicesDrYinYangRootOptions,
                        LegacyRealmTransferServicesDrYinYangAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await ApplyLegacyRealmTransferServicesDrYinYangGenderMutationAsync(
                        context,
                        session,
                        optionId,
                        source,
                        sourceLines,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 2:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "To make gender reassignment simple, you must be single, not married.",
                            "Handle your clan and guild affairs first. After reassignment, your name is removed from charm rankings and your garment is put into inventory.",
                        ],
                        [
                            new LegacyNpcDialogOption("I~want~to~change~my~gender.", 3),
                            new LegacyNpcDialogOption("I~will~think~about~it.", LegacyNpcClosedOption),
                        ],
                        LegacyRealmTransferServicesDrYinYangAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                return false;
        }
    }

    private static async ValueTask ApplyLegacyRealmTransferServicesDrYinYangGenderMutationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        string source,
        string sourceLines,
        CancellationToken cancellationToken)
    {
        var spouse = ReadAttributeString(session.Character.Attributes, "Spouse");
        if (spouse.Length == 0)
        {
            spouse = "None";
        }

        if (!string.Equals(spouse, "None", StringComparison.Ordinal))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["You`re married. To change your gender, you need to get a divorce, first."],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyRealmTransferServicesDrYinYangAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RealmTransferServices DrYinYang gender mutation " +
                    $"{FormatSession(context)} source={source} sourceLines=26403-26409 optionId={optionId} reason=married.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.ConquerPoints < LegacyRealmTransferServicesDrYinYangConquerPointCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["Gender reassignment requires 1,075 CPs. Make sure you have enough money with you."],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyRealmTransferServicesDrYinYangAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RealmTransferServices DrYinYang gender mutation " +
                    $"{FormatSession(context)} source={source} sourceLines=26410-26410,26453-26456 optionId={optionId} " +
                    $"reason=insufficient-conquer-points conquerPoints={session.Character.ConquerPoints} " +
                    $"requiredConquerPoints={LegacyRealmTransferServicesDrYinYangConquerPointCost}.")
                .ConfigureAwait(false);
            return;
        }

        if (session.PlayerItems.EquipmentItems.Any(
                static item => item.Position is LegacyGarmentEquipmentPosition or LegacyAlternateGarmentEquipmentPosition))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    source,
                    ["Please remove your garment."],
                    [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                    LegacyRealmTransferServicesDrYinYangAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RealmTransferServices DrYinYang gender mutation " +
                    $"{FormatSession(context)} source={source} sourceLines=26412-26416 optionId={optionId} " +
                    "reason=garment-equipped equipmentPositions=9,59.")
                .ConfigureAwait(false);
            return;
        }

        if (session.LegacyTradeActive)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc RealmTransferServices DrYinYang gender mutation " +
                    $"{FormatSession(context)} source={source} sourceLines=26417-26417 optionId={optionId} " +
                    "reason=in-trade; source branch is silent.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices DrYinYang gender mutation " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=session-store-missing; no character persistence or session mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.UsersRoot))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices DrYinYang gender mutation " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    "reason=users-root-missing; no character persistence or session mutation occurred.")
                .ConfigureAwait(false);
            return;
        }

        var previousBody = session.Character.Body;
        var updatedBody = previousBody == 1_008 ? 2_007 : 1_008;
        var updatedCharacter = session.Character with
        {
            Body = updatedBody,
            ConquerPoints = checked(
                session.Character.ConquerPoints - LegacyRealmTransferServicesDrYinYangConquerPointCost),
        };
        var updatedSession = session with { Character = updatedCharacter };
        var characterUid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
        var mesh = ComputeLegacyRealmTransferServicesDrYinYangMesh(updatedCharacter);
        var meshFrame = LegacyMsgUpdatePacket.CreateLegacyRuntimeMeshUpdate(characterUid, mesh).ToFrameBytes();
        var globalTalk = LegacyMsgMessagePacket
            .SystemTalk($"{updatedCharacter.Name}, Changed He's/Hers mesh into male / female.")
            .ToFrameBytes();

        bool persisted;
        string persistencePath;
        string persistenceReason;
        try
        {
            persisted = CharacterSnapshotScalarPersistence.TryPersist(
                session.UsersRoot,
                updatedCharacter,
                out persistencePath,
                out persistenceReason);
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException)
        {
            persisted = false;
            persistencePath = string.Empty;
            persistenceReason = ex.GetType().Name;
        }

        if (!persisted)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc RealmTransferServices DrYinYang gender mutation persistence " +
                    $"{FormatSession(context)} source={source} sourceLines={sourceLines} optionId={optionId} " +
                    $"reason={persistenceReason} path={persistencePath}; no session mutation or packets were committed.")
                .ConfigureAwait(false);
            return;
        }

        sessions.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendConquerPointsUpdateAsync(updatedContext, updatedCharacter, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(meshFrame, cancellationToken).ConfigureAwait(false);
        await SendPlayerSpawnToVisiblePeersAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);

        foreach (var active in sessions.Snapshot())
        {
            await active.Connection.SendAsync(globalTalk, cancellationToken).ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied LegacyNpc RealmTransferServices DrYinYang gender mutation " +
                $"{FormatSession(updatedContext)} source={source} sourceLines=26419-26450 optionId={optionId} " +
                $"body={previousBody}->{updatedBody} conquerPointsDebited={LegacyRealmTransferServicesDrYinYangConquerPointCost} " +
                $"mesh={mesh} updateOrder=ConquerPoints,MeshSelf,ReSendView,GlobalTalk.")
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-NOTE LegacyNpc RealmTransferServices DrYinYang sourceLines=26432-26448 " +
                "deliberatelyUnmodeledCacheFanout=Nobility.Gender,Nobility.Mesh,GuildMember.Mesh," +
                "ArenaStatistic.Mesh,TeamArenaStatistic.Mesh; no non-authoritative cache state was invented.")
            .ConfigureAwait(false);
    }

    private static ulong ComputeLegacyRealmTransferServicesDrYinYangMesh(CharacterSnapshot character)
    {
        var transformationId = ReadAttributeUInt64(character.Attributes, "TransformationID");
        return checked(
            (transformationId * 10_000_000UL) +
            (checked((ulong)character.Face) * 10_000UL) +
            checked((ulong)character.Body));
    }
}
