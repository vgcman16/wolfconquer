#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    // Complete native receiver bodies only: no Packet attributes, host dispatcher
    // registration or inference that these legacy opcodes are authoritative for D13.
    private static void ProcessMsgCombatGear(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        LegacyNativeMsgCombatGear obj = new LegacyNativeMsgCombatGear();
        obj = stream.ProtoBufferDeserialize(obj);
        LegacyNativeMsgCombatGear.TypeID type = obj.Type;
        LegacyNativeMsgCombatGear.TypeID typeID = type;
        if (typeID == LegacyNativeMsgCombatGear.TypeID.View && LegacyNativePoolClients.GamePoll.TryGetValue(obj.UID, out var value))
        {
            user.Send(LegacyNativeMsgCombatGear.Create(value.CombatGear, LegacyNativeMsgCombatGear.TypeID.View, value.Player.UID, value.Player.Class));
        }
    }

    private static void ProcessAirPowerUp(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        LegacyNativeMsgAirPowerUp obj = new LegacyNativeMsgAirPowerUp();
        obj = stream.ProtoBufferDeserialize(obj);
        foreach (byte[] item in LegacyNativeMsgAirPowerUp.Process(obj, user.CombatGear, user.Player.UID))
        {
            user.Send(item);
        }
        if (user.CombatGear.AirPowers.SenseLevel > 1)
        {
            user.MySpells.Add(stream, 21720, (ushort)user.CombatGear.AirPowers.SenseLevel, 0, 0);
        }
        if (user.CombatGear.AirPowers.ArmedLevel > 1)
        {
            user.MySpells.Add(stream, 21710, (ushort)user.CombatGear.AirPowers.ArmedLevel, 0, 0);
        }
        if (user.CombatGear.AirPowers.OverLoadLevel > 1)
        {
            user.MySpells.Add(stream, 21490, (ushort)user.CombatGear.AirPowers.OverLoadLevel, 0, 0);
        }
        user.CombatGear.UpdateRank();
    }
}
