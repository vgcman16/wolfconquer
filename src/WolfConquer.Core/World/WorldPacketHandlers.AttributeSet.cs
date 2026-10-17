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

    [PacketHandler(WorldPacketOpcodes.LegacyAttributeSet)]
    private static async ValueTask HandleLegacyAttributeSetAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyAttributeSetPacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyAttributeSet packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyAttributeSet {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        var total = packet.Total;
        if (session.Character.AttributePoints == 0 || session.Character.AttributePoints < total)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyAttributeSet " +
                    $"{FormatSession(context)} requested={total} available={session.Character.AttributePoints}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedCharacter = WithLegacyAttributeSet(session.Character, packet);
        context.Sessions?.Set(context.Connection, session with { Character = updatedCharacter });

        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyAttributeSetRefreshAsync(context, session, updatedCharacter, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyAttributeSet " +
                $"{FormatSession(context)} strength={packet.Strength} agility={packet.Agility} " +
                $"vitality={packet.Vitality} spirit={packet.Spirit}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyAttributeSetRefreshAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CharacterSnapshot updatedCharacter,
        CancellationToken cancellationToken)
    {
        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
        var currentHitpoints = CheckedUInt32(updatedCharacter.MinHitPoints, nameof(updatedCharacter.MinHitPoints));
        var maxHitpoints = ComputeLegacyRuntimeMaxHitpoints(
            updatedCharacter,
            currentHitpoints,
            session.PlayerItems,
            session.CombatGear ?? CombatGearSnapshot.Empty,
            context.WorldData,
            session.AlternateEquipmentActive);

        foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
            uid,
            maxHitpoints,
            Math.Min(currentHitpoints, maxHitpoints)))
        {
            await context.Connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(
                LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(uid, session.PlayerItems).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static CharacterSnapshot WithLegacyAttributeSet(
        CharacterSnapshot character,
        LegacyAttributeSetPacket packet)
    {
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Strength"] = checked(character.Strength + packet.Strength).ToString(CultureInfo.InvariantCulture),
            ["Agility"] = checked(character.Agility + packet.Agility).ToString(CultureInfo.InvariantCulture),
            ["Vitaliti"] = checked(character.Vitality + packet.Vitality).ToString(CultureInfo.InvariantCulture),
            ["Spirit"] = checked(character.Spirit + packet.Spirit).ToString(CultureInfo.InvariantCulture),
            ["Atributes"] = checked(character.AttributePoints - packet.Total).ToString(CultureInfo.InvariantCulture),
        };

        return character with
        {
            Strength = checked(character.Strength + packet.Strength),
            Agility = checked(character.Agility + packet.Agility),
            Vitality = checked(character.Vitality + packet.Vitality),
            Spirit = checked(character.Spirit + packet.Spirit),
            AttributePoints = checked(character.AttributePoints - packet.Total),
            Attributes = attributes,
        };
    }

}
