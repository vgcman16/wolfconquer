namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly LegacyNpcDialogOption[] LegacyGuildWarJailRootOptions =
    [
        new("Huh?", 3),
        new("Let's wait.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyGuildWarJailPardonOptions =
    [
        new("Leave jail.", 3),
        new("I want to stay.", LegacyNpcClosedOption),
    ];

    private static ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleGwJailAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var now = (context.TimeProvider ?? TimeProvider.System).GetLocalNow();
        return LegacyDisCityBattlefieldServicesTryHandleGwJailAsync(
            context,
            session,
            optionId,
            now,
            cancellationToken);
    }

    internal static async ValueTask<bool> LegacyDisCityBattlefieldServicesTryHandleGwJailAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var source = new LegacyDisCityBattlefieldServicesSource("NpcHandler.GWJail", "NpcHandler.cs:29735-29789");
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source.SourceMethod,
                        ["Welcome to Guild War jail. Pardons are issued during specific five-minute windows."],
                        LegacyGuildWarJailRootOptions,
                        LegacyDisCityBattlefieldServicesDefaultAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await LegacyDisCityBattlefieldServicesSendGuildWarJailPardonAsync(
                        context,
                        source,
                        now,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                var exitSession = LegacyDisCityBattlefieldServicesReviveGuildWarJailSession(session, now);
                await SendLegacyNpcTeleportAsync(
                        context,
                        exitSession,
                        LegacyDisCityBattlefieldServicesGwJailNpcId,
                        optionId,
                        LegacyDisCityBattlefieldServicesTwinCityDestination(source.SourceMethod),
                        cancellationToken,
                        canTeleport: true,
                        preflightSession: session)
                    .ConfigureAwait(false);
                return true;
            case 1:
                return true;
            default:
                return false;
        }
    }

    internal static bool LegacyDisCityBattlefieldServicesIsGuildWarJailPardonOpen(int minute)
    {
        return minute is >= 0 and <= 5 or >= 20 and <= 25 or >= 40 and <= 45;
    }

    private static async ValueTask LegacyDisCityBattlefieldServicesSendGuildWarJailPardonAsync(
        WorldPacketHandlerContext context,
        LegacyDisCityBattlefieldServicesSource source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var isOpen = LegacyDisCityBattlefieldServicesIsGuildWarJailPardonOpen(now.Minute);
        var text = isOpen
            ? "It is time for a Guild War pardon. You may leave the jail now."
            : "Rest for a while. Pardons are available at minutes 00-05, 20-25, and 40-45.";
        var options = isOpen ? LegacyGuildWarJailPardonOptions : LegacyDisCityBattlefieldServicesCloseOptions;

        await SendLegacyNpcDialogAsync(
                context,
                source.SourceMethod,
                [text],
                options,
                LegacyDisCityBattlefieldServicesDefaultAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc GuildWarJail pardon dialog " +
                $"{FormatSession(context)} source={source.SourceMethod} sourceLines={source.SourceLines} " +
                $"npcId={LegacyDisCityBattlefieldServicesGwJailNpcId} optionId=2 minute={now.Minute} open={isOpen}.")
            .ConfigureAwait(false);
    }

    private static WorldSession LegacyDisCityBattlefieldServicesReviveGuildWarJailSession(
        WorldSession session,
        DateTimeOffset now)
    {
        if (session.Character.MinHitPoints >= 1)
        {
            return session;
        }

        var revived = LegacyPlayerDeathState.TryRevive(
            session.Character,
            now,
            new LegacyPlayerReviveOptions { IgnoreReviveDelay = true });
        return revived.Allowed ? session with { Character = revived.Character } : session;
    }
}
