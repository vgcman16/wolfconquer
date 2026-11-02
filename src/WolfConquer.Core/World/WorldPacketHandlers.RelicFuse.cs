using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRelicFuseSource = "RelicFuse.Process";

    [PacketHandler(WorldPacketOpcodes.LegacyRelicFuse)]
    private static async ValueTask HandleLegacyRelicFuseAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyRelicFusePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyRelicFuse packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyRelicFuse " +
                    $"{FormatSession(context)} type={packet.Type}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        switch (packet.Type)
        {
            case LegacyRelicFusePacket.ComposeType:
                await HandleLegacyRelicFuseComposeAsync(context, session, packet, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case LegacyRelicFusePacket.ReturnType:
                await HandleLegacyRelicFuseReturnAsync(context, session, packet, cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyRelicFuse unknown type " +
                        $"{FormatSession(context)} source={LegacyRelicFuseSource} type={packet.Type} " +
                        $"relic1={packet.RelicID1} relic2={packet.RelicID2} relic3={packet.RelicID3}.")
                    .ConfigureAwait(false);
                break;
        }
    }

    // Source-only joint integration with the actual registered issuer, journal,
    // native and holder commit. Qualification of that complete owner is required.
    private static async ValueTask HandleLegacyRelicFuseComposeAsync(
        WorldPacketHandlerContext context, WorldSession session,
        LegacyRelicFusePacket packet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryCommitOwnedRelicFuse(context, session, packet, cancellationToken,
            out var committed, out var wire, out var reason))
        {
            await context.Log.WriteLineAsync(
                "WorldServer held LegacyRelicFuse compose " +
                $"{FormatSession(context)} source={LegacyRelicFuseSource} " +
                $"relic1={packet.RelicID1} relic2={packet.RelicID2} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }
        await PublishOwnedRelicWireAsync(context, wire!, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
            "WorldServer committed LegacyRelicFuse compose " +
            $"{FormatSession(context with { Session = committed })} source={LegacyRelicFuseSource} " +
            $"relic1={packet.RelicID1} relic2={packet.RelicID2}.")
            .ConfigureAwait(false);
    }

    // Return retains one existing identity through the distinct actual
    // journal/native/holder owner. No generic Set/save fallback is permitted.
    private static async ValueTask HandleLegacyRelicFuseReturnAsync(
        WorldPacketHandlerContext context, WorldSession session,
        LegacyRelicFusePacket packet, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryCommitOwnedRelicFuseReturn(context, session, packet, cancellationToken,
            out var committed, out var wire, out var reason))
        {
            await context.Log.WriteLineAsync(
                "WorldServer held LegacyRelicFuse return " +
                $"{FormatSession(context)} source={LegacyRelicFuseSource} " +
                $"relic1={packet.RelicID1} reason={reason}.").ConfigureAwait(false);
            return;
        }
        await PublishOwnedRelicWireAsync(context, wire!, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
            "WorldServer committed LegacyRelicFuse return " +
            $"{FormatSession(context with { Session = committed })} source={LegacyRelicFuseSource} " +
            $"relic1={packet.RelicID1}.").ConfigureAwait(false);
    }

    internal static bool IsAllowedLegacyRelicAttribute(uint itemId, LegacyChiAttributeType type)
    {
        return itemId switch
        {
            4_100_001 => type is not
                LegacyChiAttributeType.SkillCriticalStrike and not
                LegacyChiAttributeType.CriticalStrike and not
                LegacyChiAttributeType.HPAdd and not
                LegacyChiAttributeType.AddAttack,
            4_100_002 => type is not
                LegacyChiAttributeType.SkillCriticalStrike and not
                LegacyChiAttributeType.CriticalStrike and not
                LegacyChiAttributeType.AddMagicAttack and not
                LegacyChiAttributeType.AddAttack,
            4_100_003 => type is not
                LegacyChiAttributeType.HPAdd and not
                LegacyChiAttributeType.CriticalStrike and not
                LegacyChiAttributeType.AddMagicAttack and not
                LegacyChiAttributeType.AddAttack,
            4_100_004 => type is not
                LegacyChiAttributeType.HPAdd and not
                LegacyChiAttributeType.CriticalStrike and not
                LegacyChiAttributeType.AddMagicAttack and not
                LegacyChiAttributeType.SkillCriticalStrike,
            4_100_005 => type is not
                LegacyChiAttributeType.HPAdd and not
                LegacyChiAttributeType.AddAttack and not
                LegacyChiAttributeType.AddMagicAttack and not
                LegacyChiAttributeType.SkillCriticalStrike,
            _ => true,
        };
    }

}
