using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacyGuildProcessSilverDonateAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyGuildProcessPacket packet,
            CancellationToken cancellationToken)
    {
        const uint minimumDonation = 10_000;
        var amount = packet.Uid;
        var character = session.Character;
        var guildId = ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId");
        if (amount < minimumDonation ||
            guildId == 0 ||
            character.Money < amount)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess SilverDonate " +
                    $"{FormatSession(context)} amount={amount} guildId={guildId} money={character.Money}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedMoney = checked(character.Money - amount);
        var updatedDonation = checked(ReadAttributeUInt64(character.Attributes, "GuildDontion", "GuildDonation", "MyDontion") + amount);
        var updatedCharacter = WithCharacterAttribute(
            character with { Money = updatedMoney },
            "GuildDontion",
            updatedDonation);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyGuildProcessSilverDonateAsync(updatedContext, updatedSession).ConfigureAwait(false);

        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                LegacyMsgUpdateDataType.Money,
                CheckedUInt64(updatedMoney, nameof(updatedMoney)),
                cancellationToken)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                LegacyMsgUpdateDataType.GuildDonation,
                updatedDonation,
                cancellationToken)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                LegacyMsgUpdateDataType.GuildDonationMirror,
                updatedDonation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyGuildProcess " +
                $"{FormatSession(updatedContext)} action={packet.Action} amount={amount} " +
                $"guildId={guildId} money={updatedMoney} guildDonation={updatedDonation}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryPersistLegacyGuildProcessSilverDonateAsync(
            WorldPacketHandlerContext context,
            WorldSession session)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = session.Character.Money.ToString(CultureInfo.InvariantCulture),
            ["GuildDontion"] = ReadAttributeUInt64(session.Character.Attributes, "GuildDontion", "GuildDonation", "MyDontion")
                .ToString(CultureInfo.InvariantCulture),
        };

        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess SilverDonate persistence " +
                    $"{FormatSession(context)} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyGuildProcess SilverDonate " +
                $"{FormatSession(context)} path={Path.GetFileName(path)} keys=Money,GuildDontion.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyGuildProcessCpDonateAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            LegacyGuildProcessPacket packet,
            CancellationToken cancellationToken)
    {
        const uint minimumDonation = 1;
        const uint legacyConquerPointsUpdateType = 0x1D;
        var amount = packet.Uid;
        var character = session.Character;
        var guildId = ReadAttributeUInt64(character.Attributes, "GuildID", "GuildId");
        if (amount < minimumDonation ||
            guildId == 0 ||
            character.ConquerPoints < amount)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess CpDonate " +
                    $"{FormatSession(context)} amount={amount} guildId={guildId} conquerPoints={character.ConquerPoints}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedConquerPoints = checked(character.ConquerPoints - amount);
        var updatedDonation = checked(ReadAttributeUInt64(character.Attributes, "GuildDontion", "GuildDonation", "MyDontion") + amount);
        var updatedCharacter = WithCharacterAttribute(
            WithCharacterAttribute(
                character with { ConquerPoints = updatedConquerPoints },
                "ConquerPoints",
                CheckedUInt64(updatedConquerPoints, nameof(updatedConquerPoints))),
            "GuildDontion",
            updatedDonation);
        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyGuildProcessCpDonateAsync(updatedContext, updatedSession).ConfigureAwait(false);

        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                (LegacyMsgUpdateDataType)legacyConquerPointsUpdateType,
                CheckedUInt64(updatedConquerPoints, nameof(updatedConquerPoints)),
                cancellationToken)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                LegacyMsgUpdateDataType.GuildDonation,
                updatedDonation,
                cancellationToken)
            .ConfigureAwait(false);
        await SendScalarUpdateAsync(
                context,
                updatedCharacter,
                LegacyMsgUpdateDataType.GuildDonationMirror,
                updatedDonation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyGuildProcess " +
                $"{FormatSession(updatedContext)} action={packet.Action} amount={amount} " +
                $"guildId={guildId} conquerPoints={updatedConquerPoints} guildDonation={updatedDonation}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask TryPersistLegacyGuildProcessCpDonateAsync(
            WorldPacketHandlerContext context,
            WorldSession session)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConquerPoints"] = session.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
            ["GuildDontion"] = ReadAttributeUInt64(session.Character.Attributes, "GuildDontion", "GuildDonation", "MyDontion")
                .ToString(CultureInfo.InvariantCulture),
        };

        if (!TryPersistLegacyCharacterAttributes(session.UsersRoot, session.AccountId, values, out var path, out var reason))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyGuildProcess CpDonate persistence " +
                    $"{FormatSession(context)} reason={reason}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                "WorldServer persisted LegacyGuildProcess CpDonate " +
                $"{FormatSession(context)} path={Path.GetFileName(path)} keys=ConquerPoints,GuildDontion.")
            .ConfigureAwait(false);
    }
}
