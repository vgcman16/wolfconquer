using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int EndFlyLegacyHeavensWrathFlag = 0xEC;

    private static readonly int[] EndFlyLegacyStatusFlags =
    [
        LegacyFlyFlag,
        LegacyInfinityFlag,
        EndFlyLegacyHeavensWrathFlag,
    ];

    private static async ValueTask HandleEndFlyActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction EndFly; no active world session.")
                .ConfigureAwait(false);
            return;
        }

        var removedFly = HasLegacyStatusFlag(session.Character, LegacyFlyFlag);
        var removedInfinity = HasLegacyStatusFlag(session.Character, LegacyInfinityFlag);
        var removedHeavensWrath = HasLegacyStatusFlag(session.Character, EndFlyLegacyHeavensWrathFlag);
        var removedHeavensWrathAttribute = session.Character.Attributes.ContainsKey("HeavensWrath");
        var removedStatusFlag = removedFly || removedInfinity || removedHeavensWrath;

        var updatedSession = session;
        if (removedStatusFlag || removedHeavensWrathAttribute)
        {
            var updatedCharacter = removedStatusFlag
                ? WithoutLegacyStatusFlags(session.Character, EndFlyLegacyStatusFlags)
                : session.Character;
            if (removedHeavensWrathAttribute)
            {
                updatedCharacter = WithoutCharacterAttribute(updatedCharacter, "HeavensWrath");
            }

            updatedSession = session with { Character = updatedCharacter };
            context.Sessions?.Set(context.Connection, updatedSession);
        }

        var updatedContext = context with { Session = updatedSession };
        if (removedStatusFlag)
        {
            await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction EndFly " +
                $"{FormatSession(updatedContext)} typeId={(ushort)packet.Type} objId={packet.ObjId} " +
                $"dwParam={packet.DwParam} removedFly={removedFly} removedInfinity={removedInfinity} " +
                $"removedHeavensWrath={removedHeavensWrath || removedHeavensWrathAttribute}; " +
                "source=MsgDataPacket.EndFly [Data(120)] removes Fly, Infinity, and HeavensWrath.")
            .ConfigureAwait(false);
    }
}
