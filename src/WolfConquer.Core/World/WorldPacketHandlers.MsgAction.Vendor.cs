using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyVendorUidStart = 100_000;
    private const int LegacyVendorBoothMesh = 406;
    private const int LegacyVendorBoothKind = 14;
    private const string RuntimeVendorUidAttribute = "RuntimeVendorUid";
    private const string RuntimeVendorMapIdAttribute = "RuntimeVendorMapId";
    private const string RuntimeVendorXAttribute = "RuntimeVendorX";
    private const string RuntimeVendorYAttribute = "RuntimeVendorY";

    private static async ValueTask HandleStartVendorActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is null || context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction StartVendor; no active world session store.")
                .ConfigureAwait(false);
            return;
        }

        WorldSession? updatedSession = null;
        LegacyVendorState vendorState = default;
        SobNpc? vendorBooth = null;
        var reason = string.Empty;
        lock (LegacyVendorMutationGate)
        {
            if (!sessions.TryGet(context.Connection, out var currentSession) || currentSession is null)
            {
                reason = "current session is missing";
            }
            else if (TryReadLegacyVendorState(currentSession.Character, out var existingVendor))
            {
                reason = $"vendorUid={existingVendor.Uid}; source only creates a booth when client.IsVendor is false";
            }
            else if (TryCreateLegacyVendorBooth(
                    currentSession.Character,
                    out vendorBooth,
                    out vendorState,
                    out reason))
            {
                var updatedCharacter = WithLegacyVendorState(currentSession.Character, vendorState);
                updatedSession = currentSession with
                {
                    Character = updatedCharacter,
                    LegacyVendorListings = LegacyVendorListingBook.Empty,
                };
                sessions.Set(context.Connection, updatedSession);
            }
        }

        if (updatedSession is null || vendorBooth is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction StartVendor " +
                    $"{FormatSession(context)}; {reason}.")
                .ConfigureAwait(false);
            return;
        }

        var updatedContext = context with { Session = updatedSession };

        var spawnFrame = LegacySobNpcSpawnPacket.FromSobNpc(vendorBooth).ToFrameBytes();
        var visiblePeerCount = await SendLegacyVendorFrameToVisibleAsync(
                updatedContext,
                updatedSession,
                vendorState,
                spawnFrame,
                cancellationToken)
            .ConfigureAwait(false);

        var response = packet with
        {
            DwParam = vendorState.Uid,
            PositionX = checked((uint)vendorState.X),
            PositionY = checked((uint)vendorState.Y),
        };
        await context.Connection.SendAsync(response.ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction StartVendor " +
                $"{FormatSession(updatedContext)} vendorUid={vendorState.Uid} " +
                $"x={vendorState.X} y={vendorState.Y} visiblePlayerCount={visiblePeerCount}; " +
                "source=MsgDataPacket.StartVendor [Data(111)] creates Vendor booth mesh=406 type=Booth(14), sends SobNpc spawn, and echoes ActionCreate(data).")
            .ConfigureAwait(false);
    }

    private static async ValueTask HandleStopVendingActionAsync(
        WorldPacketHandlerContext context,
        MsgActionPacket packet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Session is null || context.Sessions is not { } sessions)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction StopVending; no active world session store.")
                .ConfigureAwait(false);
            return;
        }

        WorldSession? vendingSession = null;
        WorldSession? updatedSession = null;
        LegacyVendorState vendorState = default;
        lock (LegacyVendorMutationGate)
        {
            if (sessions.TryGet(context.Connection, out var currentSession) &&
                currentSession is not null &&
                TryReadLegacyVendorState(currentSession.Character, out vendorState))
            {
                vendingSession = currentSession;
                updatedSession = currentSession with
                {
                    Character = WithoutLegacyVendorState(currentSession.Character),
                    LegacyVendorListings = null,
                };
                sessions.Set(context.Connection, updatedSession);
            }
        }

        if (vendingSession is null || updatedSession is null)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgAction StopVending " +
                    $"{FormatSession(context)}; source only stops when client.IsVendor is true.")
                .ConfigureAwait(false);
            return;
        }

        var removeFrame = LegacyRemoveEntityPacket.FromEntity(vendorState.Uid).ToFrameBytes();
        var visiblePeerCount = await SendLegacyVendorFrameToVisibleAsync(
                context with { Session = vendingSession },
                vendingSession,
                vendorState,
                removeFrame,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgAction StopVending " +
                $"{FormatSession(context with { Session = updatedSession })} vendorUid={vendorState.Uid} " +
                $"x={vendorState.X} y={vendorState.Y} visiblePlayerCount={visiblePeerCount}; " +
                "source=MsgDataPacket.StopVending [Data(114)] sends RemoveEntity type=135, leaves the map, clears items, and clears MyVendor.")
            .ConfigureAwait(false);
    }

    private static bool TryCreateLegacyVendorBooth(
        CharacterSnapshot character,
        out SobNpc vendorBooth,
        out LegacyVendorState vendorState,
        out string reason)
    {
        vendorBooth = default!;
        vendorState = default;
        reason = string.Empty;

        if (character.Uid is < 0 or > uint.MaxValue)
        {
            reason = "owner uid is outside UInt32 range";
            return false;
        }

        if (character.MapId is < 0 or > int.MaxValue)
        {
            reason = "map id is outside Int32 range";
            return false;
        }

        var boothX = character.X + 1;
        if (boothX is < 0 or > ushort.MaxValue ||
            character.Y is < 0 or > ushort.MaxValue)
        {
            reason = "vendor booth coordinates are outside UInt16 range";
            return false;
        }

        var vendorUid = CreateLegacyVendorUid((uint)character.Uid);
        var mapId = checked((int)character.MapId);
        var x = checked((int)boothX);
        var y = checked((int)character.Y);
        vendorState = new LegacyVendorState(vendorUid, mapId, x, y);
        vendorBooth = new SobNpc(
            checked((int)vendorUid),
            character.Name,
            LegacyVendorBoothKind,
            LegacyVendorBoothMesh,
            mapId,
            x,
            y,
            Life: 0,
            MaxLife: 0,
            Unknown9: 0,
            Unknown10: null,
            LocalizedName: null,
            SourceLine: 0,
            RawLine: string.Empty,
            Fields: []);
        return true;
    }

    private static uint CreateLegacyVendorUid(uint ownerUid)
    {
        const uint vendorUidSpan = int.MaxValue - LegacyVendorUidStart;
        return LegacyVendorUidStart + (ownerUid % vendorUidSpan);
    }

    private static async ValueTask<int> SendLegacyVendorFrameToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyVendorState vendorState,
        byte[] frame,
        CancellationToken cancellationToken)
    {
        var visiblePeerCount = 0;
        if (context.Sessions is { } sessions)
        {
            foreach (var active in sessions.Snapshot())
            {
                if (ReferenceEquals(active.Connection, context.Connection) ||
                    active.Session.Character.Uid == session.Character.Uid ||
                    !CanSeeLegacyVendorBooth(active.Session.Character, vendorState))
                {
                    continue;
                }

                await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                visiblePeerCount++;
            }
        }

        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        return visiblePeerCount;
    }

    private static bool CanSeeLegacyVendorBooth(
        CharacterSnapshot character,
        LegacyVendorState vendorState)
    {
        return character.MapId == vendorState.MapId &&
            LegacyGetDistance(character.X, character.Y, vendorState.X, vendorState.Y) <= LegacyViewThreshold;
    }

    private static CharacterSnapshot WithLegacyVendorState(
        CharacterSnapshot character,
        LegacyVendorState vendorState)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeVendorUidAttribute] = vendorState.Uid.ToString(CultureInfo.InvariantCulture),
            [RuntimeVendorMapIdAttribute] = vendorState.MapId.ToString(CultureInfo.InvariantCulture),
            [RuntimeVendorXAttribute] = vendorState.X.ToString(CultureInfo.InvariantCulture),
            [RuntimeVendorYAttribute] = vendorState.Y.ToString(CultureInfo.InvariantCulture),
        };

        return character with { Attributes = attributes };
    }

    private static CharacterSnapshot WithoutLegacyVendorState(CharacterSnapshot character)
    {
        var attributes = new Dictionary<string, string>(
            character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        attributes.Remove(RuntimeVendorUidAttribute);
        attributes.Remove(RuntimeVendorMapIdAttribute);
        attributes.Remove(RuntimeVendorXAttribute);
        attributes.Remove(RuntimeVendorYAttribute);
        return character with { Attributes = attributes };
    }

    private static bool TryReadLegacyVendorState(
        CharacterSnapshot character,
        out LegacyVendorState vendorState)
    {
        vendorState = default;
        if (!TryReadUInt64(character.Attributes, out var rawUid, RuntimeVendorUidAttribute) ||
            rawUid == 0 ||
            rawUid > int.MaxValue)
        {
            return false;
        }

        var mapId = TryReadInt64Attribute(character.Attributes, RuntimeVendorMapIdAttribute, out var rawMapId)
            ? rawMapId
            : character.MapId;
        var x = TryReadInt64Attribute(character.Attributes, RuntimeVendorXAttribute, out var rawX)
            ? rawX
            : character.X + 1;
        var y = TryReadInt64Attribute(character.Attributes, RuntimeVendorYAttribute, out var rawY)
            ? rawY
            : character.Y;
        if (mapId is < 0 or > int.MaxValue ||
            x is < 0 or > ushort.MaxValue ||
            y is < 0 or > ushort.MaxValue)
        {
            return false;
        }

        vendorState = new LegacyVendorState((uint)rawUid, checked((int)mapId), checked((int)x), checked((int)y));
        return true;
    }

    private readonly record struct LegacyVendorState(uint Uid, int MapId, int X, int Y);
}
