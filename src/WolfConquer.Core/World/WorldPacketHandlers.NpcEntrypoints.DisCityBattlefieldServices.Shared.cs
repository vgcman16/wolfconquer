using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyDisCityBattlefieldServicesDefaultAvatarId = 63;
    private const ushort LegacyDisCityBattlefieldServicesDisCityAvatarId = 67;
    private const ushort LegacyDisCityBattlefieldServicesEventAvatarId = 154;

    private static readonly LegacyNpcDialogOption[] LegacyDisCityBattlefieldServicesCloseOptions =
    [
        new("I understand.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<WorldSession> LegacyDisCityBattlefieldServicesSendMoneyUpdateAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyDisCityBattlefieldServicesSource source,
        long money,
        CancellationToken cancellationToken)
    {
        var updatedCharacter = session.Character with { Money = money };
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);

        if (TryPersistLegacyCharacterAttributes(
                session.UsersRoot,
                session.AccountId,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Money"] = updatedCharacter.Money.ToString(CultureInfo.InvariantCulture),
                },
                out var path,
                out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer persisted LegacyNpc DisCityBattlefieldServices money update " +
                    $"{FormatSession(context with { Session = updatedSession })} source={source.SourceMethod} " +
                    $"path={path} keys=Money.")
                .ConfigureAwait(false);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc DisCityBattlefieldServices money persistence " +
                    $"{FormatSession(context with { Session = updatedSession })} source={source.SourceMethod} " +
                    $"reason={reason}.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                        CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid)),
                        [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Money, checked((ulong)updatedCharacter.Money))])
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc DisCityBattlefieldServices money update " +
                $"{FormatSession(context)} source={source.SourceMethod} sourceLines={source.SourceLines} " +
                $"npcId={npcId} optionId={optionId} money={money}.")
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static async ValueTask LegacyDisCityBattlefieldServicesSendUnavailableAsync(
        WorldPacketHandlerContext context,
        uint npcId,
        byte optionId,
        LegacyDisCityBattlefieldServicesSource source,
        string text,
        string reason,
        ushort avatarId,
        CancellationToken cancellationToken)
    {
        await SendLegacyNpcDialogAsync(
                context,
                source.SourceMethod,
                [text],
                LegacyDisCityBattlefieldServicesCloseOptions,
                avatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc DisCityBattlefieldServices " +
                $"{FormatSession(context)} source={source.SourceMethod} sourceLines={source.SourceLines} " +
                $"npcId={npcId} optionId={optionId}; {reason}")
            .ConfigureAwait(false);
    }

    private static LegacyNpcTeleportDestination LegacyDisCityBattlefieldServicesTwinCityDestination(string sourceMethod)
    {
        return new LegacyNpcTeleportDestination(1_002, 410, 354, sourceMethod);
    }

    private readonly record struct LegacyDisCityBattlefieldServicesSource(
        string SourceMethod,
        string SourceLines);
}
