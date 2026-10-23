using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed class LegacyNativeSelfStatusBindings
{
    private readonly LegacyNativeGameMap? map;
    private readonly LegacyNativeMapView? mapView;
    private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? pool;
    private readonly uint mapId, dynamicId;
    private readonly IntPtr callerAddress;
    private readonly int callerCapacity, callerSize, callerPosition;
    private readonly byte[] callerBytes;

    internal LegacyNativeGameClientRuntime Client { get; }
    internal LegacyNativePlayerRuntime Player { get; }
    internal LegacyNativeStatusFlagsBigVector32 Vector { get; }
    internal LegacyNativeRoleView View { get; }
    internal LegacyNativePacket Caller { get; }
    internal uint Uid { get; }
    internal ushort X { get; }
    internal ushort Y { get; }

    internal unsafe LegacyNativeSelfStatusBindings(LegacyNativeGameClientRuntime client,
        LegacyNativePlayerRuntime player, LegacyNativeStatusFlagsBigVector32 vector,
        LegacyNativeRoleView view, LegacyNativePacket caller)
    {
        Client = client; Player = player; Vector = vector; View = view; Caller = caller;
        Uid = player.UID; X = player.X; Y = player.Y; mapId = player.Map; dynamicId = player.DynamicID;
        map = client.Map; mapView = map?.View; pool = LegacyNativePoolClients.GamePoll;
        callerAddress = (IntPtr)caller.Memory; callerCapacity = caller.Capacity;
        callerSize = caller.Size; callerPosition = caller.Position;
        if (callerAddress == IntPtr.Zero || callerCapacity < 4 || callerSize < 0 || callerSize > callerCapacity ||
            callerPosition < 0 || callerPosition > callerCapacity)
            throw new InvalidDataException("Self-status caller has no valid live packet storage.");
        callerBytes = new byte[callerCapacity];
        Marshal.Copy(callerAddress, callerBytes, 0, callerBytes.Length);
    }

    internal bool IsCurrent() => ReferenceEquals(Client.Player, Player) && ReferenceEquals(Player.Owner, Client) &&
        ReferenceEquals(Player.BitVector, Vector) && ReferenceEquals(Player.View, View) && ReferenceEquals(View.Owner, Client) &&
        Player.UID == Uid && Player.X == X && Player.Y == Y && Player.Map == mapId && Player.DynamicID == dynamicId &&
        ReferenceEquals(Client.Map, map) && (map is null || (mapView is not null && ReferenceEquals(map.View, mapView))) &&
        pool is not null && ReferenceEquals(LegacyNativePoolClients.GamePoll, pool);

    internal unsafe bool HasCallerStorage(int required) => (IntPtr)Caller.Memory == callerAddress &&
        callerAddress != IntPtr.Zero && Caller.Capacity == callerCapacity && required >= 4 && required <= callerCapacity;

    internal unsafe bool HasOriginalCaller(int required)
    {
        if (!HasCallerStorage(required) || Caller.Size != callerSize || Caller.Position != callerPosition) return false;
        for (int index = 0; index < callerBytes.Length; index++)
            if (Caller.Memory[index] != callerBytes[index]) return false;
        return true;
    }

    // Take/Reuse only move a cursor. This repairs our rejected alias borrow,
    // never an unrelated raw byte/Size mutation or a disposed/replaced allocation.
    internal void RestoreRejectedBorrowCursor()
    {
        if (HasCallerStorage(4) && Caller.Size == callerSize) Caller.Seek(callerPosition);
    }
}
