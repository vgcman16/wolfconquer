using System.Globalization;
using System.Net;
using System.Net.Sockets;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

internal static partial class Program
{
    static int LegacyLootRoundtripSmoke(string[] args)
    {
        LegacyLootRoundtripSmokeOptions options;
        try { options = ResolveLegacyLootRoundtripSmokeOptions(args); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            RunLegacyLootRoundtripSmokeAsync(options).GetAwaiter().GetResult();
            Console.WriteLine($"result=PASS mode={(options.VerifyOnly ? "verify-only" : "roundtrip")} profile=VisibleDx8D13");
            Console.WriteLine("boundary=encrypted-local-WorldServer-wire-and-relogin; not client-rendering, AccountServer-auth, or SQL proof");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or SocketException or OperationCanceledException)
        {
            Console.Error.WriteLine("Legacy loot roundtrip smoke failed: " + ex.Message);
            return 2;
        }
    }

    static LegacyLootRoundtripSmokeOptions ResolveLegacyLootRoundtripSmokeOptions(string[] args)
    {
        const string usage = "Usage: WolfConquer.DataTool legacy-loot-roundtrip-smoke <loopback-ip> <port> <account-key> <character-uid> <item-uid> [--timeout-ms ms] [--verify-only]";
        if (args.Length < 6) throw new ArgumentException(usage);
        if (!IPAddress.TryParse(args[1], out var address) || !IPAddress.IsLoopback(address))
            throw new ArgumentException("A literal loopback IP address is required; DNS and external addresses are rejected.");
        if (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
            throw new ArgumentException("Port must be an integer from 1 through 65535.");
        if (port == 5818) throw new ArgumentException("Port 5818 is reserved for the live client stack; use a disposable WorldServer port.");
        uint ReadId(int index, string name) => uint.TryParse(args[index], NumberStyles.None,
            CultureInfo.InvariantCulture, out var id) && id != 0 ? id :
            throw new ArgumentException(name + " must be a nonzero UInt32.");
        var accountKey = ReadId(3, "Account key");
        var characterUid = ReadId(4, "Character UID");
        var itemUid = ReadId(5, "Item UID");
        if (accountKey != characterUid)
            throw new ArgumentException("This backend's legacy account key must equal the character UID.");
        var timeoutMs = 15_000;
        var timeoutSet = false;
        var verifyOnly = false;
        for (var index = 6; index < args.Length; index++)
        {
            if (args[index] == "--verify-only" && !verifyOnly) verifyOnly = true;
            else if (args[index] == "--timeout-ms" && !timeoutSet)
            {
                timeoutSet = true;
                if (++index >= args.Length || !int.TryParse(args[index], NumberStyles.None,
                        CultureInfo.InvariantCulture, out timeoutMs) || timeoutMs is < 1000 or > 120_000)
                    throw new ArgumentException("--timeout-ms must be from 1000 through 120000.");
            }
            else throw new ArgumentException("Unknown or duplicate legacy-loot-roundtrip-smoke option: " + args[index]);
        }

        return new(address, port, accountKey, characterUid, itemUid, TimeSpan.FromMilliseconds(timeoutMs), verifyOnly);
    }

    static async Task RunLegacyLootRoundtripSmokeAsync(LegacyLootRoundtripSmokeOptions options)
    {
        using var timeout = new CancellationTokenSource(options.Timeout);
        var phase = "connect";
        try
        {
            LegacyGameItemPacket original;
            using (var wire = await LegacyLootRoundtripWire.ConnectAsync(options, timeout.Token).ConfigureAwait(false))
            {
                phase = "startup";
                var startup = await ReadLegacyLootStartupAsync(wire, options, timeout.Token).ConfigureAwait(false);
                original = RequireLegacyLootInventoryItem(startup, options.ItemUid);
                Console.WriteLine($"phase=startup-ok characterUid={options.CharacterUid} itemUid={original.Uid} itemId={original.ItemId}");
                if (options.VerifyOnly)
                {
                    phase = "disconnect";
                    await wire.FinishAsync(timeout.Token).ConfigureAwait(false);
                    Console.WriteLine("phase=verify-only-ok noDrop=true");
                    return;
                }

                if (original.Bound != 0 || original.Locked != 0 || original.StackSize == 0 ||
                    startup.Inventory.Values.Any(item => item.Uid != original.Uid && item.ItemId == original.ItemId))
                    throw new InvalidDataException("Choose an unbound, unlocked positive-stack inventory item with no same-type merge target.");
                phase = "drop";
                var floor = await DropLegacyLootItemAsync(wire, options, original, timeout.Token).ConfigureAwait(false);
                Console.WriteLine($"phase=drop-ok floorUid={floor.Uid} itemUid={original.Uid} itemId={original.ItemId}");
                phase = "pickup";
                await PickUpLegacyLootItemAsync(wire, options, original, floor, timeout.Token).ConfigureAwait(false);
                Console.WriteLine($"phase=pickup-ok floorUid={floor.Uid} itemUid={original.Uid}");
                phase = "disconnect";
                await wire.FinishAsync(timeout.Token).ConfigureAwait(false);
            }

            phase = "relogin";
            using var reconnected = await LegacyLootRoundtripWire.ConnectAsync(options, timeout.Token).ConfigureAwait(false);
            var reloaded = RequireLegacyLootInventoryItem(
                await ReadLegacyLootStartupAsync(reconnected, options, timeout.Token).ConfigureAwait(false), options.ItemUid);
            if (reloaded.ItemId != original.ItemId || reloaded.StackSize != original.StackSize)
                throw new InvalidDataException("Relogin inventory did not retain the original item identity and stack.");
            phase = "relogin-disconnect";
            await reconnected.FinishAsync(timeout.Token).ConfigureAwait(false);
            Console.WriteLine($"phase=relogin-ok itemUid={reloaded.Uid} itemId={reloaded.ItemId} stack={reloaded.StackSize}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or SocketException or OperationCanceledException)
        {
            throw new InvalidDataException($"phase={phase}: {ex.Message}", ex);
        }
    }

    static async Task<LegacyLootStartupState> ReadLegacyLootStartupAsync(
        LegacyLootRoundtripWire wire, LegacyLootRoundtripSmokeOptions options, CancellationToken token)
    {
        var startup = new LegacyLootStartupState(options.CharacterUid);
        // Ping is a real ordered request/echo, not an idle-time guess. This first
        // barrier drains all LoginGame startup before the separate Data132 phase.
        await wire.BarrierAsync(startup.Observe, token).ConfigureAwait(false);
        if (!startup.AnswerOk || !startup.Hero || !startup.ServerVersion || !startup.D13Chi)
            throw new InvalidDataException("Startup lacks ANSWER_OK, matching hero UID, ServerVer, or explicit D13 chi data.");
        await wire.SendAsync(new MsgActionPacket
        {
            ObjId = options.CharacterUid, Type = MsgActionType.CompleteLogin,
            Timestamp = unchecked((uint)Environment.TickCount),
        }.ToFrameBytes(), token).ConfigureAwait(false);
        var completeResponse = false;
        await wire.BarrierAsync(frame =>
        {
            startup.Observe(frame);
            if (frame.Header.Opcode != LegacyUserAbilityScorePacket.Opcode) return;
            if (!LegacyUserAbilityScorePacket.TryParse(frame, out var score) ||
                score.Type != LegacyUserAbilityScorePacket.ResponseType || score.Uid != options.CharacterUid)
                throw new InvalidDataException("Malformed or wrong-owner Data132 ability-score response.");
            completeResponse = true;
        }, token).ConfigureAwait(false);
        if (!completeResponse) throw new InvalidDataException("CompleteLogin132 did not produce its ability-score response before the next ping.");
        return startup;
    }

    static LegacyGameItemPacket RequireLegacyLootInventoryItem(LegacyLootStartupState startup, uint uid)
    {
        if (!startup.Inventory.TryGetValue(uid, out var item) || item.Position != 0 || item.ItemId == 0)
            throw new InvalidDataException($"Requested inventory item UID {uid} is missing from this connection's startup.");
        return item;
    }

    static async Task<LegacyFloorItemPacket> DropLegacyLootItemAsync(LegacyLootRoundtripWire wire,
        LegacyLootRoundtripSmokeOptions options, LegacyGameItemPacket original, CancellationToken token)
    {
        await wire.SendAsync(new MsgItemUsagePacket(original.Uid, 0, MsgItemUsageAction.DropItem,
            0, 0, 0, 0).ToFrameBytes(), token).ConfigureAwait(false);
        LegacyFloorItemPacket? floor = null;
        var removed = false;
        while (floor is null || !removed)
        {
            var frame = await wire.ReadAsync(token).ConfigureAwait(false);
            if (LegacyFloorItemPacket.TryParse(frame, out var candidate))
            {
                if (candidate.DropType != LegacyFloorItemDropType.Visible || candidate.ItemId != original.ItemId ||
                    candidate.ItemOwnerUid != options.CharacterUid || floor is not null)
                    throw new InvalidDataException("Drop returned an unexpected floor item or owner.");
                floor = candidate;
            }
            if (MsgItemUsagePacket.TryParse(frame, out var usage) && usage.Action == MsgItemUsageAction.RemoveInventory)
            {
                if (usage.Id != original.Uid || removed || floor is null)
                    throw new InvalidDataException("Drop removed an unexpected inventory UID or removed it before floor visibility.");
                removed = true;
            }
        }
        return floor;
    }

    static async Task PickUpLegacyLootItemAsync(LegacyLootRoundtripWire wire,
        LegacyLootRoundtripSmokeOptions options, LegacyGameItemPacket original,
        LegacyFloorItemPacket floor, CancellationToken token)
    {
        var request = new PacketWriter(LegacyFloorItemPacket.Opcode, sizeof(uint));
        request.WriteUInt32(0, floor.Uid);
        await wire.SendAsync(request.ToArray(), token).ConfigureAwait(false);
        var itemSeen = false;
        var floorRemoved = false;
        var textSeen = false;
        while (!itemSeen || !floorRemoved || !textSeen)
        {
            var frame = await wire.ReadAsync(token).ConfigureAwait(false);
            if (LegacyGameItemPacket.TryParse(frame, out var item))
            {
                if (item.Uid != original.Uid || item.ItemId != original.ItemId || item.Position != 0 ||
                    item.StackSize != original.StackSize || floorRemoved)
                    throw new InvalidDataException("Pickup returned a different item UID, type, position, or stack.");
                itemSeen = true;
            }
            if (LegacyFloorItemPacket.TryParse(frame, out var removed))
            {
                if (removed.DropType != LegacyFloorItemDropType.Remove || removed.Uid != floor.Uid ||
                    removed.ItemId != original.ItemId || removed.ItemOwnerUid != options.CharacterUid ||
                    !itemSeen || floorRemoved)
                    throw new InvalidDataException("Pickup removed an unexpected floor identity or removed it before item creation.");
                floorRemoved = true;
            }
            if (LegacyMsgMessagePacket.TryParse(frame, out var message) &&
                message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                message.Content[3].StartsWith("You have picked up a ", StringComparison.Ordinal))
            {
                if (!floorRemoved || textSeen)
                    throw new InvalidDataException("Pickup success text preceded floor removal or was duplicated.");
                textSeen = true;
            }
        }
        await wire.BarrierAsync(_ => { }, token).ConfigureAwait(false);
    }

    private sealed class LegacyLootStartupState(uint uid)
    {
        internal bool AnswerOk, Hero, ServerVersion, D13Chi;
        internal Dictionary<uint, LegacyGameItemPacket> Inventory { get; } = new();

        internal void Observe(PacketFrame frame)
        {
            if (LegacyMsgMessagePacket.TryParse(frame, out var message) && message.Content[3] == "ANSWER_OK") AnswerOk = true;
            if (frame.Header.Opcode == LegacyServerVersionPacket.Opcode) ServerVersion = true;
            if (LegacyLoaderHeroInfoPacket.TryParse(frame, out var hero))
            {
                if (hero.Uid != uid) throw new InvalidDataException("Startup hero UID does not match the requested character.");
                Hero = true;
            }
            if (VisibleDx8D13TrainingVitalityInfoPacket.TryParse(frame, out var chi))
            {
                if (chi.Uid != uid) throw new InvalidDataException("D13 startup chi data belongs to another character.");
                D13Chi = true;
            }
            if (!LegacyGameItemPacket.TryParse(frame, out var item) || item.Position != 0) return;
            if (Inventory.TryGetValue(item.Uid, out var before) && before.ItemId != item.ItemId)
                throw new InvalidDataException("Startup reused an inventory UID for another item type.");
            Inventory[item.Uid] = item;
        }
    }
}

internal sealed record LegacyLootRoundtripSmokeOptions(IPAddress Address, int Port, uint AccountKey,
    uint CharacterUid, uint ItemUid, TimeSpan Timeout, bool VerifyOnly);
