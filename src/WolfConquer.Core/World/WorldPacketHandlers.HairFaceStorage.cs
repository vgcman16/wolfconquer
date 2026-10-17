using System.Globalization;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{

    [PacketHandler(WorldPacketOpcodes.LegacyHairFaceStorage)]
    private static async ValueTask HandleLegacyHairFaceStorageAsync(
            WorldPacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!LegacyHairFaceStoragePacket.TryParse(frame, out var packet))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer rejected malformed LegacyHairFaceStorage packet {FormatSession(context)}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyHairFaceStorage {FormatSession(context)} action={packet.Action}.")
            .ConfigureAwait(false);

        if (packet.Action == LegacyHairFaceStorageAction.Buy)
        {
            await HandleLegacyHairFaceStorageBuyAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyHairFaceStorageAction.Equip)
        {
            await HandleLegacyHairFaceStorageEquipAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyHairFaceStorageAction.BuyColor)
        {
            await HandleLegacyHairFaceStorageBuyColorAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (packet.Action == LegacyHairFaceStorageAction.EquipColor)
        {
            await HandleLegacyHairFaceStorageEquipColorAsync(context, packet, cancellationToken).ConfigureAwait(false);
            return;
        }

        LegacyHairFaceStoragePacket? response = packet.Action switch
        {
            LegacyHairFaceStorageAction.Login => CreateLegacyHairFaceStorageLoginResponse(context),
            LegacyHairFaceStorageAction.Avatars or LegacyHairFaceStorageAction.HairStyles =>
                LegacyHairFaceStoragePacket.TabResponse(packet.Action),
            _ => null,
        };

        if (response is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage mutation action {FormatSession(context)} action={packet.Action}.")
                .ConfigureAwait(false);
            return;
        }

        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyHairFaceStorage {FormatSession(context)} action={response.Action}.")
            .ConfigureAwait(false);
    }

    private static LegacyHairFaceStoragePacket CreateLegacyHairFaceStorageLoginResponse(
            WorldPacketHandlerContext context)
    {
        if (context.Session is not { } session)
        {
            return LegacyHairFaceStoragePacket.EmptyLoginResponse();
        }

        return new LegacyHairFaceStoragePacket(
            LegacyHairFaceStorageAction.Login,
            Hair: null,
            HairColor: null,
            AvatarsOrHairTap: [],
            ReadLegacyHairFaceStorageItems(context.WorldData, session.Character));
    }

    private static async ValueTask HandleLegacyHairFaceStorageBuyAsync(
            WorldPacketHandlerContext context,
            LegacyHairFaceStoragePacket packet,
            CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy {FormatSession(context)}; world data is not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyHairFaceOperation(packet.Hair, out var type, out var id))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy {FormatSession(context)}; hair operation is missing or invalid.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindLegacyHairFaceStorageType(worldData, type, id, out var definition))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy {FormatSession(context)} type={type} id={id}; catalog entry was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Money < definition.Cost)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy {FormatSession(context)} type={type} id={id} cost={definition.Cost} money={session.Character.Money}.")
                .ConfigureAwait(false);
            return;
        }

        var owned = ReadLegacyHairFaceStorageItems(worldData, session.Character);
        var updatedCharacter = session.Character with { Money = session.Character.Money - definition.Cost };
        if (!owned.Any(item => item.Type == type && item.Id == id))
        {
            var purchased = CreateLegacyHairFaceStorageItem(definition);
            owned = owned.Concat([purchased]).OrderBy(item => item.Type).ThenBy(item => item.Id).ToArray();
            updatedCharacter = WithLegacyHairFaceStorageItems(updatedCharacter, owned);
            await context.Connection.SendAsync(
                    new LegacyHairFaceStoragePacket(
                        LegacyHairFaceStorageAction.Add,
                        Hair: null,
                        HairColor: null,
                        AvatarsOrHairTap: [],
                        Items: [purchased]).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        context.Sessions?.Set(context.Connection, session with { Character = updatedCharacter });
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyHairFaceStorage buy {FormatSession(context)} type={type} id={id} cost={definition.Cost}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyHairFaceStorageEquipAsync(
            WorldPacketHandlerContext context,
            LegacyHairFaceStoragePacket packet,
            CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip {FormatSession(context)}; world data is not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyHairFaceOperation(packet.Hair, out var type, out var id))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip {FormatSession(context)}; hair operation is missing or invalid.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindLegacyHairFaceStorageType(worldData, type, id, out var definition))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip {FormatSession(context)} type={type} id={id}; catalog entry was not found.")
                .ConfigureAwait(false);
            return;
        }

        var owned = ReadLegacyHairFaceStorageItems(worldData, session.Character);
        var ownedItem = owned.FirstOrDefault(item => item.Type == type && item.Id == id);
        if (ownedItem is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip {FormatSession(context)} type={type} id={id}; item is not owned.")
                .ConfigureAwait(false);
            return;
        }

        if (!CanEquipLegacyHairFaceStorageItem(session.Character, definition))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip {FormatSession(context)} type={type} id={id}; requirements are not met.")
                .ConfigureAwait(false);
            return;
        }

        var hair = CreateLegacyHairValue(id, GetLegacyHairFaceEquippedColor(ownedItem));
        var updatedCharacter = type == LegacyHairFaceStorageType.Hairstyle
            ? WithCharacterAttribute(
                session.Character with { Hair = hair },
                "Haire",
                hair.ToString(CultureInfo.InvariantCulture))
            : WithCharacterAttribute(session.Character with { Face = id }, "Face", id);
        context.Sessions?.Set(context.Connection, session with { Character = updatedCharacter });

        await context.Connection.SendAsync(
                new LegacyHairFaceStoragePacket(
                    LegacyHairFaceStorageAction.Equip,
                    new LegacyHairFaceStorageOperation((uint)type, id),
                    HairColor: null,
                    AvatarsOrHairTap: [],
                    Items: []).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyHairFaceStorage equip {FormatSession(context)} type={type} id={id}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyHairFaceStorageBuyColorAsync(
            WorldPacketHandlerContext context,
            LegacyHairFaceStoragePacket packet,
            CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy color {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy color {FormatSession(context)}; world data is not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyHairFaceColorOperation(packet.HairColor, out var id, out var colorIndex))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy color {FormatSession(context)}; hair-color operation is missing or invalid.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryFindLegacyHairColorType(worldData, id, colorIndex, out var definition))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy color {FormatSession(context)} id={id} color={colorIndex}; catalog entry was not found.")
                .ConfigureAwait(false);
            return;
        }

        if (session.Character.Money < definition.Cost)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage buy color {FormatSession(context)} id={id} color={colorIndex} cost={definition.Cost} money={session.Character.Money}.")
                .ConfigureAwait(false);
            return;
        }

        var owned = ReadLegacyHairFaceStorageItems(worldData, session.Character);
        var ownedItem = owned.FirstOrDefault(item => item.Type == LegacyHairFaceStorageType.Hairstyle && item.Id == id);
        var updatedCharacter = session.Character with { Money = session.Character.Money - definition.Cost };
        if (ownedItem is not null)
        {
            var updatedItem = SetLegacyHairFaceColorFlag(ownedItem, colorIndex);
            updatedCharacter = WithLegacyHairFaceStorageItems(
                updatedCharacter,
                ReplaceLegacyHairFaceStorageItem(owned, updatedItem));

            await context.Connection.SendAsync(
                    CreateLegacyHairFaceColorResponse(LegacyHairFaceStorageAction.AddColor, id, colorIndex)
                        .ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        context.Sessions?.Set(context.Connection, session with { Character = updatedCharacter });
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyHairFaceStorage buy color {FormatSession(context)} id={id} color={colorIndex} cost={definition.Cost}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleLegacyHairFaceStorageEquipColorAsync(
            WorldPacketHandlerContext context,
            LegacyHairFaceStoragePacket packet,
            CancellationToken cancellationToken)
    {
        if (context.Session is not { } session)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip color {FormatSession(context)}; no character session is available.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip color {FormatSession(context)}; world data is not loaded.")
                .ConfigureAwait(false);
            return;
        }

        if (!TryReadLegacyHairFaceColorOperation(packet.HairColor, out var id, out var colorIndex))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip color {FormatSession(context)}; hair-color operation is missing or invalid.")
                .ConfigureAwait(false);
            return;
        }

        var owned = ReadLegacyHairFaceStorageItems(worldData, session.Character);
        var ownedItem = owned.FirstOrDefault(item => item.Type == LegacyHairFaceStorageType.Hairstyle && item.Id == id);
        if (ownedItem is null)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip color {FormatSession(context)} id={id} color={colorIndex}; item is not owned.")
                .ConfigureAwait(false);
            return;
        }

        if (!HasLegacyHairFaceColor(ownedItem, colorIndex))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip color {FormatSession(context)} id={id} color={colorIndex}; color is not owned.")
                .ConfigureAwait(false);
            return;
        }

        if ((ulong)session.Character.Hair % 1000UL != id)
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped LegacyHairFaceStorage equip color {FormatSession(context)} id={id} color={colorIndex}; hairstyle is not equipped.")
                .ConfigureAwait(false);
            return;
        }

        var updatedItem = SetLegacyHairFaceEquippedColor(ownedItem, colorIndex);
        var hair = CreateLegacyHairValue(id, colorIndex);
        var updatedCharacter = WithLegacyHairFaceStorageItems(
            WithCharacterAttribute(
                session.Character with { Hair = hair },
                "Haire",
                hair.ToString(CultureInfo.InvariantCulture)),
            ReplaceLegacyHairFaceStorageItem(owned, updatedItem));
        context.Sessions?.Set(context.Connection, session with { Character = updatedCharacter });

        await context.Connection.SendAsync(
                CreateLegacyHairFaceColorResponse(LegacyHairFaceStorageAction.EquipColor, id, colorIndex)
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer handled LegacyHairFaceStorage equip color {FormatSession(context)} id={id} color={colorIndex}.")
            .ConfigureAwait(false);
    }

}
