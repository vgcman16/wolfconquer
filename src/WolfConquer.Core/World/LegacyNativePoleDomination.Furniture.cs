using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativePoleDomination
{
    // Whole native54..57. Native lazy map lookup is not replaced by a callback.
    public void CreateFurnitures()
    {
        Furnitures.Add(LegacyNativeSobNpcRuntime.StaticMesh.Pole,
            LegacyNativePoolMaps.ServerMaps![11083u].View.GetMapObject<LegacyNativeSobNpcRuntime>(
                LegacyNativeMapObjectType.SobNpc, 830u));
    }

    // Whole native59..86. Reset all HP before starting recipient iteration.
    internal void ResetFurnitures(LegacyNativePacket stream)
    {
        foreach (LegacyNativeSobNpcRuntime value in Furnitures.Values)
        {
            value.HitPoints = value.MaxHitPoints;
        }
        foreach (LegacyNativeGameClientRuntime value2 in LegacyNativePoolClients.GamePoll!.Values)
        {
            if (value2.Player.Map != 11083)
            {
                continue;
            }
            foreach (LegacyNativeSobNpcRuntime value3 in Furnitures.Values)
            {
                if (LegacyNativeRoleCore.GetDistance(value2.Player.X, value2.Player.Y, value3.X, value3.Y) <= 19)
                {
                    LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, value3.UID);
                    msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.Hitpoints, value3.HitPoints);
                    stream = msgUpdate.GetArray(stream);
                    value2.Send(stream);
                    if (value3.Mesh == LegacyNativeSobNpcRuntime.StaticMesh.Pole)
                    {
                        value2.Send(value3.GetArray(stream, view: false, st: false));
                    }
                }
            }
        }
    }

    // Whole native88..97. Dynamic ID is intentionally not filtered.
    internal void SendMapPacket(LegacyNativePacket packet)
    {
        foreach (LegacyNativeGameClientRuntime value in LegacyNativePoolClients.GamePoll!.Values)
        {
            if (value.Player.Map == 11083 || value.Player.Map == 6001)
            {
                value.Send(packet);
            }
        }
    }
}
