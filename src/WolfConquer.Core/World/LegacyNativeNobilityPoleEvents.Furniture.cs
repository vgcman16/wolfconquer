using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgNobilityPole
{
    public static void Load()
    {
        Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.Pole, LegacyNativePoolMaps.ServerMaps![MapID].View!.GetMapObject<LegacyNativeSobNpcRuntime>(LegacyNativeMapObjectType.SobNpc, 22340u)!);
    }

    public static void Reset(LegacyNativePacket stream)
    {
        NobilityPoleList = new LegacyNativeDbSafeDictionary<uint, Info>();
        foreach (LegacyNativeSobNpcRuntime value in Furnitures.Values)
        {
            value.HitPoints = value.MaxHitPoints;
        }
        LegacyNativeSobNpcRuntime Pole = Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole];
        LegacyNativeGameClientRuntime[] array = LegacyNativePoolClients.GamePoll!.Values.Where((LegacyNativeGameClientRuntime u) => LegacyNativeRoleCore.GetDistance(u.Player.X, u.Player.Y, Pole.X, Pole.Y) <= 19).ToArray();
        if (array == null)
        {
            return;
        }
        LegacyNativeGameClientRuntime[] array2 = array;
        foreach (LegacyNativeGameClientRuntime gameClient in array2)
        {
            LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, Pole.UID, 2);
            stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Mesh, (long)Pole.Mesh);
            stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Hitpoints, Pole.HitPoints);
            stream = msgUpdate.GetArray(stream);
            gameClient.Send(stream);
            if (Pole.Mesh == LegacyNativeSobNpcRuntime.StaticMesh.Pole)
            {
                gameClient.Send(Pole.GetArray(stream, view: false, st: false));
            }
        }
    }

    public static void SendMapPacket(LegacyNativePacket packet)
    {
        LegacyNativeGameClientRuntime[] values = LegacyNativePoolMaps.ServerMaps![MapID].Values;
        foreach (LegacyNativeGameClientRuntime gameClient in values)
        {
            gameClient.Send(packet);
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole1
{
    public static void Load()
    {
        Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.Pole, LegacyNativePoolMaps.ServerMaps![MapID].View!.GetMapObject<LegacyNativeSobNpcRuntime>(LegacyNativeMapObjectType.SobNpc, 22341u)!);
    }

    public static void Reset(LegacyNativePacket stream)
    {
        NobilityPoleList = new LegacyNativeDbSafeDictionary<uint, Info>();
        foreach (LegacyNativeSobNpcRuntime value in Furnitures.Values)
        {
            value.HitPoints = value.MaxHitPoints;
        }
        LegacyNativeSobNpcRuntime Pole = Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole];
        LegacyNativeGameClientRuntime[] array = LegacyNativePoolClients.GamePoll!.Values.Where((LegacyNativeGameClientRuntime u) => LegacyNativeRoleCore.GetDistance(u.Player.X, u.Player.Y, Pole.X, Pole.Y) <= 19).ToArray();
        if (array == null)
        {
            return;
        }
        LegacyNativeGameClientRuntime[] array2 = array;
        foreach (LegacyNativeGameClientRuntime gameClient in array2)
        {
            LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, Pole.UID, 2);
            stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Mesh, (long)Pole.Mesh);
            stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Hitpoints, Pole.HitPoints);
            stream = msgUpdate.GetArray(stream);
            gameClient.Send(stream);
            if (Pole.Mesh == LegacyNativeSobNpcRuntime.StaticMesh.Pole)
            {
                gameClient.Send(Pole.GetArray(stream, view: false, st: false));
            }
        }
    }

    public static void SendMapPacket(LegacyNativePacket packet)
    {
        LegacyNativeGameClientRuntime[] values = LegacyNativePoolMaps.ServerMaps![MapID].Values;
        foreach (LegacyNativeGameClientRuntime gameClient in values)
        {
            gameClient.Send(packet);
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole2
{
    public static void Load()
    {
        Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.Pole, LegacyNativePoolMaps.ServerMaps![MapID].View!.GetMapObject<LegacyNativeSobNpcRuntime>(LegacyNativeMapObjectType.SobNpc, 22342u)!);
    }

    public static void Reset(LegacyNativePacket stream)
    {
        NobilityPoleList = new LegacyNativeDbSafeDictionary<uint, Info>();
        foreach (LegacyNativeSobNpcRuntime value in Furnitures.Values)
        {
            value.HitPoints = value.MaxHitPoints;
        }
        LegacyNativeSobNpcRuntime Pole = Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole];
        LegacyNativeGameClientRuntime[] array = LegacyNativePoolClients.GamePoll!.Values.Where((LegacyNativeGameClientRuntime u) => LegacyNativeRoleCore.GetDistance(u.Player.X, u.Player.Y, Pole.X, Pole.Y) <= 19).ToArray();
        if (array == null)
        {
            return;
        }
        LegacyNativeGameClientRuntime[] array2 = array;
        foreach (LegacyNativeGameClientRuntime gameClient in array2)
        {
            LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, Pole.UID, 2);
            stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Mesh, (long)Pole.Mesh);
            stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Hitpoints, Pole.HitPoints);
            stream = msgUpdate.GetArray(stream);
            gameClient.Send(stream);
            if (Pole.Mesh == LegacyNativeSobNpcRuntime.StaticMesh.Pole)
            {
                gameClient.Send(Pole.GetArray(stream, view: false, st: false));
            }
        }
    }

    public static void SendMapPacket(LegacyNativePacket packet)
    {
        LegacyNativeGameClientRuntime[] values = LegacyNativePoolMaps.ServerMaps![MapID].Values;
        foreach (LegacyNativeGameClientRuntime gameClient in values)
        {
            gameClient.Send(packet);
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole3
{
    public static void Load()
    {
        Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.Pole, LegacyNativePoolMaps.ServerMaps![MapID].View!.GetMapObject<LegacyNativeSobNpcRuntime>(LegacyNativeMapObjectType.SobNpc, 22343u)!);
    }

    public static void Reset(LegacyNativePacket stream)
    {
        NobilityPoleList = new LegacyNativeDbSafeDictionary<uint, Info>();
        foreach (LegacyNativeSobNpcRuntime value in Furnitures.Values)
        {
            value.HitPoints = value.MaxHitPoints;
        }
        LegacyNativeSobNpcRuntime Pole = Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole];
        LegacyNativeGameClientRuntime[] array = LegacyNativePoolClients.GamePoll!.Values.Where((LegacyNativeGameClientRuntime u) => LegacyNativeRoleCore.GetDistance(u.Player.X, u.Player.Y, Pole.X, Pole.Y) <= 19).ToArray();
        if (array == null)
        {
            return;
        }
        LegacyNativeGameClientRuntime[] array2 = array;
        foreach (LegacyNativeGameClientRuntime gameClient in array2)
        {
            LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, Pole.UID, 2);
            stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Mesh, (long)Pole.Mesh);
            stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Hitpoints, Pole.HitPoints);
            stream = msgUpdate.GetArray(stream);
            gameClient.Send(stream);
            if (Pole.Mesh == LegacyNativeSobNpcRuntime.StaticMesh.Pole)
            {
                gameClient.Send(Pole.GetArray(stream, view: false, st: false));
            }
        }
    }

    public static void SendMapPacket(LegacyNativePacket packet)
    {
        LegacyNativeGameClientRuntime[] values = LegacyNativePoolMaps.ServerMaps![MapID].Values;
        foreach (LegacyNativeGameClientRuntime gameClient in values)
        {
            gameClient.Send(packet);
        }
    }
}
