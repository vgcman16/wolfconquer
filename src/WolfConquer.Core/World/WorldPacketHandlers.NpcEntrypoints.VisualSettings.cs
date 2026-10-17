using System.Globalization;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyWeatherNpcId = 45_465;
    private const uint LegacyHideEffectNpcId = 747_462;
    private const ushort LegacyVisualSettingsAvatarId = 286;
    private const string LegacyWeatherTypeAttribute = "weatherType";
    private const string LegacyColorAttribute = "color";
    private const string LegacySpawnEffectHiddenAttribute = "LegacySpawnEffectHidden";
    private const string LegacySkillEffectHiddenAttribute = "LegacySkillEffectHidden";

    private static readonly LegacyNpcDialogOption[] LegacyWeatherRootOptions =
    [
        new("Weather", 33),
        new("Color", 34),
        new("Never mind.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyWeatherWeatherOptions =
    [
        new("Rain", 1),
        new("Snow", 2),
        new("Rain and wind", 3),
        new("Cherry blossom petals", 4),
        new("Cherry blossom petals with wind", 5),
        new("Blowing cotton", 6),
        new("Atoms", 7),
        new("None", 8),
        new("No thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyWeatherColorOptions =
    [
        new("Black", 9),
        new("Red", 10),
        new("Silver", 11),
        new("White", 12),
        new("No thanks.", LegacyNpcClosedOption),
    ];

    public static async ValueTask<bool> TryHandleLegacyVisualSettingsNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        return npcId switch
        {
            LegacyWeatherNpcId => await TryHandleLegacyWeatherNpcAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            LegacyHideEffectNpcId => await TryHandleLegacyHideEffectNpcAsync(
                    context,
                    session,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false),
            _ => false,
        };
    }

    private static async ValueTask<bool> TryHandleLegacyWeatherNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.Weather",
                        ["I can change the weather and colors around you. What would you like?"],
                        LegacyWeatherRootOptions,
                        LegacyVisualSettingsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 33:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.Weather",
                        ["Would you like special weather? You can choose any of these for free."],
                        LegacyWeatherWeatherOptions,
                        LegacyVisualSettingsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 34:
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.Weather",
                        ["Would you like a special color? You can choose any of these for free."],
                        LegacyWeatherColorOptions,
                        LegacyVisualSettingsAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
        }

        if (TryResolveLegacyWeather(optionId, out var weatherType))
        {
            await SendLegacyWeatherSelectionAsync(context, session, optionId, weatherType, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryResolveLegacyColor(optionId, out var color))
        {
            await SendLegacyColorSelectionAsync(context, session, optionId, color, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async ValueTask<bool> TryHandleLegacyHideEffectNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyHideEffectRootAsync(context, session, cancellationToken).ConfigureAwait(false);
                return true;
            case 33:
                await ToggleLegacyHiddenEffectAsync(
                        context,
                        session,
                        LegacySpawnEffectHiddenAttribute,
                        "ground item",
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 34:
                await ToggleLegacyHiddenEffectAsync(
                        context,
                        session,
                        LegacySkillEffectHiddenAttribute,
                        "skill",
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask SendLegacyHideEffectRootAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var spawnHidden = ReadAttributeBool(session.Character.Attributes, LegacySpawnEffectHiddenAttribute);
        var skillHidden = ReadAttributeBool(session.Character.Attributes, LegacySkillEffectHiddenAttribute);
        var options = new[]
        {
            new LegacyNpcDialogOption(spawnHidden ? "Show ground item effects." : "Hide ground item effects.", 33),
            new LegacyNpcDialogOption(skillHidden ? "Show skill effects." : "Hide skill effects.", 34),
            new LegacyNpcDialogOption("No thanks.", LegacyNpcClosedOption),
        };

        await SendLegacyNpcDialogAsync(
                context,
                "NpcHandler.HideEffect",
                ["Do you want to hide some visual effects? You can choose the setting you want for free."],
                options,
                LegacyVisualSettingsAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask ToggleLegacyHiddenEffectAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string attribute,
        string label,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var wasHidden = ReadAttributeBool(session.Character.Attributes, attribute);
        var nowHidden = !wasHidden;
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(session.Character, attribute, nowHidden ? "1" : "0"),
        };
        context.Sessions?.Set(context.Connection, updatedSession);

        var text = nowHidden
            ? $"You have hidden {label} effects."
            : $"You can now see {label} effects.";
        await SendLegacyNpcDialogAsync(
                context,
                "NpcHandler.HideEffect",
                [text],
                [],
                avatarId: null,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer toggled LegacyNpc HideEffect " +
                $"{FormatSession(context)} option={optionId} attribute={attribute} hidden={nowHidden}; " +
                "source fields SpawinEffect/SpawinEffectSkills are modeled as transient character attributes.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyWeatherSelectionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        LegacyWeatherType weatherType,
        CancellationToken cancellationToken)
    {
        var packet = new LegacyWeatherPacket(weatherType, 250, 250, 250, 250);
        await context.Connection.SendAsync(packet.ToFrameBytes(), cancellationToken).ConfigureAwait(false);

        var updatedSession = session with
        {
            Character = WithCharacterAttribute(session.Character, LegacyWeatherTypeAttribute, (ulong)weatherType),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc Weather packet " +
                $"{FormatSession(context)} option={optionId} weatherType={(uint)weatherType}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyColorSelectionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        uint color,
        CancellationToken cancellationToken)
    {
        var character = session.Character;
        var action = new MsgActionPacket
        {
            ObjId = CheckedUInt32(character.Uid, nameof(character.Uid)),
            Type = MsgActionType.LegacyColor,
            DwParam = color,
            PositionX = CheckedUInt32(character.X, nameof(character.X)),
            PositionY = CheckedUInt32(character.Y, nameof(character.Y)),
        };

        await context.Connection.SendAsync(action.ToFrameBytes(), cancellationToken).ConfigureAwait(false);
        var updatedSession = session with
        {
            Character = WithCharacterAttribute(character, LegacyColorAttribute, color.ToString(CultureInfo.InvariantCulture)),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc Weather color action " +
                $"{FormatSession(context)} option={optionId} color={color}.")
            .ConfigureAwait(false);
    }

    private static bool TryResolveLegacyWeather(byte optionId, out LegacyWeatherType weatherType)
    {
        weatherType = optionId switch
        {
            1 => LegacyWeatherType.Rain,
            2 => LegacyWeatherType.Snow,
            3 => LegacyWeatherType.RainWind,
            4 => LegacyWeatherType.CherryBlossomPetals,
            5 => LegacyWeatherType.CherryBlossomPetalsWind,
            6 => LegacyWeatherType.BlowingCotten,
            7 => LegacyWeatherType.Atoms,
            8 => LegacyWeatherType.Nothing,
            _ => default,
        };

        return optionId is >= 1 and <= 8;
    }

    private static bool TryResolveLegacyColor(byte optionId, out uint color)
    {
        color = optionId switch
        {
            9 => 5_855_577,
            10 => 16_755_370,
            11 => 14_535_867,
            12 => 0,
            _ => 0,
        };

        return optionId is >= 9 and <= 12;
    }
}
