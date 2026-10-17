using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyProgressionInnerPowerNpcId = 78_954;
    private const uint LegacyProgressionSupClassNpcId = 78_964;
    private const long LegacyProgressionInnerPowerPointsCost = 100_000;
    private const long LegacyProgressionInnerPowerFullCost = 250_000_000;
    private const long LegacyProgressionSupClassCost = 10_000_000;
    private const ushort LegacyProgressionOfferAvatarId = 265;
    private const ushort LegacyProgressionGateAvatarId = 261;
    private const ushort LegacyProgressionInsufficientCpAvatarId = 166;
    private const uint LegacyProgressionInnerPowerPointsItemId = 3_007_110;

    private static readonly LegacyNpcDialogOption[] LegacyProgressionInnerPowerOptions =
    [
        new("Fix~InnerPower.", 1),
        new("InnerPower~points.", 2),
        new("No~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyProgressionSupClassOptions =
    [
        new("Fix~Supclass.", 1),
        new("No~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyProgressionCloseOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacyProgressionServicesNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyProgressionInnerPowerNpcId => await TryHandleLegacyProgressionInnerPowerAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyProgressionSupClassNpcId => await TryHandleLegacyProgressionSupClassAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyProgressionInnerPowerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            if (!CanUseLegacyProgressionService(session))
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.InnerPower",
                        ["Sorry, you need level 100 and second rebirth before you can use InnerPower."],
                        LegacyProgressionCloseOptions,
                        LegacyProgressionGateAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.InnerPower",
                    [
                        $"Hello, {session.Character.Name}. I can fix your InnerPower.",
                        "Full InnerPower costs 250,000,000 CPs, or you can buy InnerPower points for 100,000 CPs.",
                    ],
                    LegacyProgressionInnerPowerOptions,
                    LegacyProgressionOfferAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 1)
        {
            if (session.Character.ConquerPoints < LegacyProgressionInnerPowerFullCost)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.InnerPower",
                        ["You do not have 250,000,000 CPs."],
                        LegacyProgressionCloseOptions,
                        6,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyProgressionUnavailableAsync(
                    context,
                    "NpcHandler.InnerPower",
                    optionId,
                    "full InnerPower mutation is not modeled safely",
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 2)
        {
            if (!HasLegacyInventorySpace(session.PlayerItems, 1))
            {
                await context.Connection.SendAsync(
                        LegacyMsgMessagePacket.BoxDialog("Please make 1 more space in your inventory.").ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (session.Character.ConquerPoints < LegacyProgressionInnerPowerPointsCost)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.InnerPower",
                        ["Please take 100.000 cps ."],
                        LegacyProgressionCloseOptions,
                        LegacyProgressionInsufficientCpAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            var conquerPointsBefore = session.Character.ConquerPoints;
            var updatedCharacter = session.Character with
            {
                ConquerPoints = checked(conquerPointsBefore - LegacyProgressionInnerPowerPointsCost),
            };
            var grantedItem = CreateLegacyNpcItemGrant(context, session.PlayerItems, LegacyProgressionInnerPowerPointsItemId);
            var updatedItems = session.PlayerItems with
            {
                InventoryItems = session.PlayerItems.InventoryItems.Concat([grantedItem]).OrderBy(item => item.Uid).ToArray(),
            };
            var updatedSession = session with
            {
                Character = updatedCharacter,
                PlayerItems = updatedItems,
            };
            context.Sessions?.Set(context.Connection, updatedSession);
            var updatedContext = context with { Session = updatedSession };

            await SendScalarUpdateAsync(
                    updatedContext,
                    updatedCharacter,
                    LegacyMsgUpdateDataType.ConquerPoints,
                    checked((ulong)updatedCharacter.ConquerPoints),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Connection.SendAsync(
                    LegacyGameItemPacket.FromSnapshotItem(grantedItem).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await SendLegacyItemFollowupsAsync(updatedContext, grantedItem, cancellationToken).ConfigureAwait(false);
            await PersistLegacyArchiveCharacterAttributesAsync(
                    updatedContext,
                    updatedSession,
                    "NpcHandler.InnerPower",
                    "ConquerPoints")
                .ConfigureAwait(false);
            await PersistLegacyNpcItemsAsync(updatedContext, grantedItem.ItemId, "NpcHandler.InnerPower").ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc InnerPower points purchase " +
                    $"{FormatSession(updatedContext)} source=NpcHandler.InnerPower itemId={grantedItem.ItemId} " +
                    $"count=1 bound={grantedItem.Bound} conquerPointsBefore={conquerPointsBefore} " +
                    $"conquerPointsAfter={updatedCharacter.ConquerPoints}.")
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyProgressionSupClassAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            if (!CanUseLegacyProgressionService(session))
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.SUpClass",
                        ["Please reach level 100 and second rebirth first."],
                        LegacyProgressionCloseOptions,
                        LegacyProgressionGateAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SUpClass",
                    [$"Hello, {session.Character.Name}. I can fix your Supclass for 10,000,000 CPs."],
                    LegacyProgressionSupClassOptions,
                    LegacyProgressionOfferAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (optionId != 1)
        {
            return false;
        }

        var currentState = ReadLegacySubClassState(session.Character);
        if (currentState.Entries.FirstOrDefault(entry => entry.Id == 5)?.Level >= 1)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.SUpClass",
                    ["Aha,~you~are~already~an~Apothecary,~remember?~I~suggest~you~focus~your~attention~on~upgrading~your~sub-class~level,~now."],
                    [new("Oh.", LegacyNpcClosedOption)],
                    284,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (session.Character.ConquerPoints < LegacyProgressionSupClassCost)
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk("You don`t have 10,00.000 ConquerPoints .").ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var entries = currentState.Entries.ToList();
        var learned = new List<LegacySubClassEntry>();
        foreach (var subclassId in LegacySupClassIds)
        {
            if (entries.Any(entry => entry.Id == subclassId))
            {
                continue;
            }

            var entry = new LegacySubClassEntry(subclassId, Level: 1, Phrase: 1);
            entries.Add(entry);
            learned.Add(entry);
        }

        var updatedState = currentState with { Entries = entries.ToArray() };
        var conquerPointsBefore = session.Character.ConquerPoints;
        var conquerPointsAfter = checked(conquerPointsBefore - LegacyProgressionSupClassCost);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                WithCharacterAttribute(
                    session.Character with { ConquerPoints = conquerPointsAfter },
                    "SubProfInfo",
                    FormatLegacySubClassState(updatedState)),
                "SubClassHashPoint",
                CalculateLegacySubClassHashPoint(updatedState).ToString(CultureInfo.InvariantCulture)),
            "ConquerPoints",
            conquerPointsAfter.ToString(CultureInfo.InvariantCulture));
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendScalarUpdateAsync(
                updatedContext,
                updatedCharacter,
                LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)updatedCharacter.ConquerPoints),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var entry in learned)
        {
            await context.Connection.SendAsync(
                    new LegacySubClassUpdatePacket(
                        LegacySubClassAction.LearnSubClass,
                        entry.Id,
                        entry.Level,
                        StudyReceive: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyStringPacket.PlayerEffect(
                    CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                    "zf2-e300").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await PersistLegacyArchiveCharacterAttributesAsync(
                updatedContext,
                updatedSession,
                "NpcHandler.SUpClass",
                "ConquerPoints",
                "SubProfInfo",
                "SubClassHashPoint")
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc SUpClass fix " +
                $"{FormatSession(updatedContext)} source=NpcHandler.SUpClass cost={LegacyProgressionSupClassCost} " +
                $"conquerPointsBefore={conquerPointsBefore} conquerPointsAfter={conquerPointsAfter} " +
                $"learned={string.Join(',', learned.Select(entry => entry.Id))} effect=zf2-e300.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool CanUseLegacyProgressionService(WorldSession session)
    {
        return session.Character.Level >= 100 && session.Character.Reborn >= 2;
    }

    private static async ValueTask SendLegacyProgressionUnavailableAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        byte optionId,
        string reason,
        CancellationToken cancellationToken)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    sourceMethod == "NpcHandler.InnerPower"
                        ? LegacyProgressionInnerPowerNpcId
                        : LegacyProgressionSupClassNpcId,
                    optionId: optionId,
                    source: sourceMethod,
                    reason: reason))
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc progression service " +
                $"{FormatSession(context)} source={sourceMethod} option={optionId}; {reason}.")
            .ConfigureAwait(false);
        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                ["That service is not available yet. Your CPs were not spent."],
                LegacyProgressionCloseOptions,
                LegacyProgressionOfferAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
