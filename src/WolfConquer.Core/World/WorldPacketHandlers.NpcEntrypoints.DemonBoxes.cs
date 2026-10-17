using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyDemonBoxUnavailableText =
        "The Box is unavailable for use in the cities and Market.";

    private static bool IsLegacyDemonBoxNpc(uint npcId)
    {
        return TryGetLegacyDemonBox(npcId, out _);
    }

    private static bool IsLegacyDemonBoxItem(uint itemId)
    {
        return TryGetLegacyDemonBoxByItemId(itemId, out _);
    }

    private static async ValueTask HandleLegacyDemonBoxItemUseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerItemSnapshotItem item,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyDemonBoxByItemId(item.ItemId, out var box))
        {
            return;
        }

        var updatedSession = session with { ActiveNpcId = box.NpcId };
        context.Sessions?.Set(context.Connection, updatedSession);
        await TryHandleLegacyDemonBoxesAsync(
                context with { Session = updatedSession },
                updatedSession,
                box.NpcId,
                optionId: 0,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> TryHandleLegacyDemonBoxesAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetLegacyDemonBox(npcId, out var box))
        {
            return false;
        }

        if (!HasLegacyInventoryStackCount(session.PlayerItems, box.ItemId, 1))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc DemonBox " +
                    $"{FormatSession(context)} source={box.SourceMethod} npcId={npcId} option={optionId} " +
                    $"itemId={box.ItemId}; source returns before dialog when the box item is missing.")
                .ConfigureAwait(false);
            return true;
        }

        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        box.SourceMethod,
                        box.IntroTexts,
                        LegacyDemonBoxIntroOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            case 1:
                await TryReleaseLegacyDemonBoxMonsterAsync(
                        context,
                        session,
                        box,
                        optionId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;

            default:
                await context.Log.WriteLineAsync(
                        "WorldServer ignored LegacyNpc DemonBox option " +
                        $"{FormatSession(context)} source={box.SourceMethod} npcId={npcId} option={optionId}; " +
                        "source switch has no matching branch.")
                    .ConfigureAwait(false);
                return true;
        }
    }

    private static async ValueTask TryReleaseLegacyDemonBoxMonsterAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyDemonBoxDefinition box,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var dynamicId = ReadAttributeUInt64(session.Character.Attributes, "DynamicID", "DynamicId");
        if (dynamicId == 0 ||
            session.Character.Uid < 0 ||
            dynamicId != checked((ulong)session.Character.Uid))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTalk(LegacyDemonBoxUnavailableText).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc DemonBox release " +
                    $"{FormatSession(context)} source={box.SourceMethod} npcId={box.NpcId} option={optionId} " +
                    $"itemId={box.ItemId} monsterId={box.MonsterId} dynamicId={dynamicId}; " +
                    "source requires client.MyHouse != null and Player.DynamicID == Player.UID.")
                .ConfigureAwait(false);
            return;
        }

        if (dynamicId > uint.MaxValue)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc DemonBox dynamic spawn " +
                    $"{FormatSession(context)} source={box.SourceMethod} npcId={box.NpcId} option={optionId} " +
                    $"itemId={box.ItemId} monsterId={box.MonsterId} dynamicId={dynamicId}; source Server.AddMapMonster " +
                    "uses a uint dynamic instance id, so the box item was preserved.")
                .ConfigureAwait(false);
            return;
        }

        var mapId = CheckedInt32(session.Character.MapId, nameof(session.Character.MapId));
        if (HasVisibleLegacyDynamicMonster(context.MonsterStates, mapId, dynamicId, box.MonsterId))
        {
            await SendLegacyDemonBoxTalkAsync(context, box.AlreadyReleasedMessage, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer handled LegacyNpc DemonBox already-released branch " +
                    $"{FormatSession(context)} source={box.SourceMethod} npcId={box.NpcId} option={optionId} " +
                    $"itemId={box.ItemId} monsterId={box.MonsterId} dynamicId={dynamicId}.")
                .ConfigureAwait(false);
            return;
        }

        if (context.WorldData is not { } worldData ||
            !worldData.MonstersById.TryGetValue(CheckedInt32(box.MonsterId, nameof(box.MonsterId)), out var monsterFamily))
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc DemonBox dynamic spawn " +
                    $"{FormatSession(context)} source={box.SourceMethod} npcId={box.NpcId} option={optionId} " +
                    $"itemId={box.ItemId} monsterId={box.MonsterId}; source Server.AddMapMonster requires monster.txt " +
                    "family data, so the box item was preserved.")
                .ConfigureAwait(false);
            return;
        }

        if (context.MonsterStates is not { } monsterStates)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc DemonBox dynamic spawn " +
                    $"{FormatSession(context)} source={box.SourceMethod} npcId={box.NpcId} option={optionId} " +
                    $"itemId={box.ItemId} monsterId={box.MonsterId}; source Server.AddMapMonster requires " +
                    "MonsterRuntimeStateStore, so the box item was preserved.")
                .ConfigureAwait(false);
            return;
        }

        var maxLife = ReadAttributeUInt32(monsterFamily.Attributes, "life");
        if (maxLife == 0)
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc DemonBox dynamic spawn " +
                    $"{FormatSession(context)} source={box.SourceMethod} npcId={box.NpcId} option={optionId} " +
                    $"itemId={box.ItemId} monsterId={box.MonsterId}; source Server.AddMapMonster requires " +
                    "monster family life, so the box item was preserved.")
                .ConfigureAwait(false);
            return;
        }

        var mutationFrames = new List<byte[]>();
        var updatedItems = ConsumeLegacyInventoryStack(session.PlayerItems, box.ItemId, 1, mutationFrames);
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await PersistLegacyDemonBoxItemsAsync(updatedContext, updatedSession, box, cancellationToken)
            .ConfigureAwait(false);

        foreach (var frame in mutationFrames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await SendLegacyDemonBoxDynamicSpawnAsync(
                updatedContext,
                monsterStates,
                monsterFamily,
                box,
                mapId,
                CheckedUInt16(session.Character.X, nameof(session.Character.X)),
                CheckedUInt16(session.Character.Y, nameof(session.Character.Y)),
                maxLife,
                checked((uint)dynamicId),
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyDemonBoxTalkAsync(updatedContext, box.ReleasedMessage, cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc DemonBox release " +
                $"{FormatSession(updatedContext)} source={box.SourceMethod} npcId={box.NpcId} option={optionId} " +
                $"itemId={box.ItemId} monsterId={box.MonsterId} dynamicId={dynamicId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyDemonBoxDynamicSpawnAsync(
        WorldPacketHandlerContext context,
        MonsterRuntimeStateStore monsterStates,
        Monster monsterFamily,
        LegacyDemonBoxDefinition box,
        int mapId,
        ushort x,
        ushort y,
        uint maxLife,
        uint dynamicId,
        CancellationToken cancellationToken)
    {
        var uid = monsterStates.NextLegacyDynamicMonsterUid();
        var instance = new LegacyMonsterInstance(
            uid,
            mapId,
            x,
            y,
            ReadAttributeUInt32(monsterFamily.Attributes, "lookface"),
            CheckedLegacyDemonBoxByte(ReadAttributeUInt32(monsterFamily.Attributes, "level"), "level"),
            maxLife,
            maxLife,
            box.MonsterId,
            monsterFamily.Name,
            ReadAttributeUInt32(monsterFamily.Attributes, "Boss") > 0,
            GeneratorId: CheckedInt32(uid, nameof(uid)),
            SpawnIndex: 0,
            RestSeconds: 0,
            DynamicId: dynamicId)
        {
            RemoveOnDead = true,
        };
        instance = monsterStates.RegisterDynamicMonster(instance, monsterFamily);

        await SendLegacyInstanceInfoMonsterAsync(context, instance, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent LegacyNpc DemonBox dynamic spawn " +
                $"{FormatSession(context)} source={box.SourceMethod} npcId={box.NpcId} " +
                $"monsterUid={instance.Uid} monsterId={instance.MonsterId} map={instance.MapId} " +
                $"x={instance.X} y={instance.Y} dynamicId={instance.DynamicId}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyDemonBoxTalkAsync(
        WorldPacketHandlerContext context,
        string message,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask PersistLegacyDemonBoxItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyDemonBoxDefinition box,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    session.Character.Uid,
                    session.PlayerItems);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc DemonBox item persistence " +
                        $"{FormatSession(context)} source={box.SourceMethod} itemId={box.ItemId} error={ex.Message}.")
                    .ConfigureAwait(false);
            }
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc DemonBox missing PlayerItemsRoot " +
                    $"{FormatSession(context)} source={box.SourceMethod}; item mutation is session-only.")
                .ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static byte CheckedLegacyDemonBoxByte(uint value, string name)
    {
        if (value > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy guest box monster value exceeds byte range.");
        }

        return (byte)value;
    }

    private static bool TryGetLegacyDemonBox(uint npcId, out LegacyDemonBoxDefinition box)
    {
        foreach (var candidate in LegacyDemonBoxes)
        {
            if (candidate.NpcId == npcId)
            {
                box = candidate;
                return true;
            }
        }

        box = default;
        return false;
    }

    private static bool TryGetLegacyDemonBoxByItemId(uint itemId, out LegacyDemonBoxDefinition box)
    {
        foreach (var candidate in LegacyDemonBoxes)
        {
            if (candidate.ItemId == itemId)
            {
                box = candidate;
                return true;
            }
        }

        box = default;
        return false;
    }

    private static readonly LegacyNpcDialogOption[] LegacyDemonBoxIntroOptions =
    [
        new("Yes!", 1),
        new("Wait...", LegacyNpcClosedOption),
    ];

    private static readonly LegacyDemonBoxDefinition[] LegacyDemonBoxes =
    [
        new(
            NpcId: 4_294_967_257,
            ItemId: 720_652,
            MonsterId: 2_422,
            SourceMethod: "NpcHandler.FloodDemonBox",
            IntroTexts:
            [
                "You should open your Demon Box in a secret place far away from the guards and other players! Do not open it at the border of the map!",
                "By killing the Flood Demon, you will have a chance to get a Fantasy CP Pack (13800 CPs). Are you sure you want to release it now?",
            ],
            AlreadyReleasedMessage: "The Flood Demon was released! Hurry and kill it to get the rewards!",
            ReleasedMessage: "The Flood Demon was released! Hurry and kill it to get the rewards!"),
        new(
            NpcId: 4_294_967_258,
            ItemId: 720_651,
            MonsterId: 2_421,
            SourceMethod: "NpcHandler.AncientDemonBox",
            IntroTexts:
            [
                "You should open your Demon Box in a secret place far away from the guards and other players! Do not open it at the border of the map!",
                "By killing the Ancient Demon, you will have a chance to get a Mystic CP Pack (6900 CPs). Are you sure you want to release it now?",
            ],
            AlreadyReleasedMessage: "The Ancient Demon was released! Hurry and kill it to get the rewards!",
            ReleasedMessage: "You opened the Ancient Demon Box and an Ancient Demon was released! Hurry and kill it to get your reward!"),
        new(
            NpcId: 4_294_967_259,
            ItemId: 720_650,
            MonsterId: 2_420,
            SourceMethod: "NpcHandler.DemonBox",
            IntroTexts:
            [
                "You should open your Demon Box in a secret place far away from the guards and other players! Do not open it at the border of the map!",
                "By killing the Demon, you will have a chance to get a Joy CP Pack (1380 CPs). Are you sure you want to release it now?",
            ],
            AlreadyReleasedMessage: "The Demon was released! Hurry and kill it to get the rewards!",
            ReleasedMessage: "You opened the Demon Box and a guest was released! Hurry and kill it to get the reward!"),
        new(
            NpcId: 4_294_967_260,
            ItemId: 720_674,
            MonsterId: 2_438,
            SourceMethod: "NpcHandler.AuroraDemonBox",
            IntroTexts:
            [
                "If you open the Aurora Demon Box, a guest will be released! After you kill it, you'll have a chance to get a Pilgrim CP Pack (690000 CPs)!",
                "I suggest you open it in a secret place to keep from being robbed. Are you sure you want to open it and release the guest now?",
            ],
            AlreadyReleasedMessage: "The Aurora Demon was released! Hurry and kill it to get the rewards!",
            ReleasedMessage: "You opened the Aurora Demon Box and a guest was released! Hurry and kill it to get the rewards!"),
        new(
            NpcId: 4_294_967_261,
            ItemId: 720_673,
            MonsterId: 2_437,
            SourceMethod: "NpcHandler.SacredDemonBox",
            IntroTexts:
            [
                "If you open the Sacred Demon Box, a guest will be released! After you kill it, you'll have a chance to get a Kylin CP Pack (276000 CPs)!",
                "I suggest you open it somewhere safe from prying eyes. Are you sure you want to open it and release the guest now?",
            ],
            AlreadyReleasedMessage: "The Sacred Demon was released! Hurry and kill it to get the rewards!",
            ReleasedMessage: "You opened the Sacred Demon Box and a Sacred Demon was released! Hurry and kill it to get the rewards!"),
        new(
            NpcId: 4_294_967_262,
            ItemId: 720_672,
            MonsterId: 2_436,
            SourceMethod: "NpcHandler.ChaosDemonBox",
            IntroTexts:
            [
                "You should open your Demon Box in a secret place far away from the guards and other players! Do not open it at the border of the map!",
                "By killing the Chaos Demon, you will have a chance to get a Nimbus CP Pack (138000 CPs). Are you sure you want to release it now?",
            ],
            AlreadyReleasedMessage: "The Chaos Demon was released! Hurry and kill it to get the rewards!",
            ReleasedMessage: "You opened the Chaos Demon Box and a Chaos Demon was released! Hurry and kill it to get the rewards!"),
        new(
            NpcId: 4_294_967_263,
            ItemId: 720_671,
            MonsterId: 2_435,
            SourceMethod: "NpcHandler.HeavenDemonBox",
            IntroTexts:
            [
                "You should open your Demon Box in a secret place far away from the guards and other players! Do not open it at the border of the map!",
                "By killing the Heaven Demon, you will have a chance to get a Frost CP Pack (69000 CPs). Are you sure you want to release it now?",
            ],
            AlreadyReleasedMessage: "The Heaven Demon was released! Hurry and kill it to get the rewards!",
            ReleasedMessage: "You opened the Heaven Demon Box and a Heaven Demon was released! Hurry and kill it to get the rewards!"),
    ];

    private readonly record struct LegacyDemonBoxDefinition(
        uint NpcId,
        uint ItemId,
        uint MonsterId,
        string SourceMethod,
        IReadOnlyList<string> IntroTexts,
        string AlreadyReleasedMessage,
        string ReleasedMessage);
}
