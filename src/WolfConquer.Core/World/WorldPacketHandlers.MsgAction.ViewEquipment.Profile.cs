using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<LegacyViewEquipmentPrestigeCounts> SendLegacyViewEquipmentPrestigeAsync(
        WorldPacketHandlerContext context,
        WorldSession target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = CreateViewEquipmentPrestigeResponse(target, context);
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        return new LegacyViewEquipmentPrestigeCounts(
            AbilityScorePacketCount: 1,
            AbilityScoreItemCount: response.Items.Count);
    }

    private static async ValueTask<LegacyViewEquipmentProfileCounts> SendLegacyViewEquipmentProfileAsync(
        WorldPacketHandlerContext context,
        WorldSession requester,
        WorldSession target,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await context.Connection.SendAsync(
                CreateViewEquipmentSpousePacket(requester, target).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                CreateViewEquipmentStatusResponse(target, context).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        return new LegacyViewEquipmentProfileCounts(
            ViewEquipSpousePacketCount: 1,
            StatusPacketCount: 1);
    }

    private static LegacyUserAbilityScorePacket CreateViewEquipmentPrestigeResponse(
        WorldSession target,
        WorldPacketHandlerContext context)
    {
        return CreateLegacyUserAbilityScoreResponse(target, context.WorldData);
    }

    private static LegacyStringPacket CreateViewEquipmentSpousePacket(
        WorldSession requester,
        WorldSession target)
    {
        var requesterUid = CheckedUInt32(requester.Character.Uid, nameof(requester.Character.Uid));
        return new LegacyStringPacket(
            requesterUid,
            LegacyStringId.ViewEquipSpouse,
            [ReadViewEquipmentSpouse(target.Character)]);
    }

    private static LegacyPlayerStatusPacket CreateViewEquipmentStatusResponse(
        WorldSession target,
        WorldPacketHandlerContext context)
    {
        return CreateLegacyPlayerStatusResponse(target, context.WorldData);
    }

    private static async ValueTask<bool> TrySendViewEquipmentNoticeAsync(
        WorldSession requester,
        WorldActiveSession? activeTarget,
        CancellationToken cancellationToken)
    {
        if (activeTarget is null ||
            !ShouldSendViewEquipmentNotice(requester.Character))
        {
            return false;
        }

        await activeTarget.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk($"{requester.Character.Name} is checking your equipment.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static bool ShouldSendViewEquipmentNotice(CharacterSnapshot requester)
    {
        return !IsViewEquipmentGameMaster(requester) ||
            IsViewEquipmentProjectManager(requester);
    }

    private static bool IsViewEquipmentGameMaster(CharacterSnapshot requester)
    {
        return requester.Name.Contains("[GM]", StringComparison.OrdinalIgnoreCase) ||
            ReadAttributeBool(requester.Attributes, "GameMaster", "IsGameMaster", "GM");
    }

    private static bool IsViewEquipmentProjectManager(CharacterSnapshot requester)
    {
        return ReadAttributeBool(requester.Attributes, "ProjectManager", "IsProjectManager");
    }

    private static string ReadViewEquipmentSpouse(CharacterSnapshot target)
    {
        return target.Attributes.TryGetValue("Spouse", out var spouse) && spouse.Length != 0
            ? spouse
            : "None";
    }
}
