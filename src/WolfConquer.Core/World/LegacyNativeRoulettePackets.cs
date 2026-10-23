using System;
using System.Linq;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeRoulettePackets
{
    public static LegacyNativePacket RoulettedAddNewPlayerCreate(this LegacyNativePacket stream, uint UID, uint Mesh, LegacyNativeMsgRouletteOpenGui.Color Color, string Name)
    {
        stream.InitWriter();
        stream.Write(UID);
        stream.Write(Mesh);
        stream.Write((byte)Color);
        stream.Write(Name, 32);
        stream.Finalize(2019);
        return stream;
    }

    public static LegacyNativePacket RouletteRecordCreate(this LegacyNativePacket stream, LegacyNativeRoulettes.RouletteTable.Member[]? members, byte WinnerNumber)
    {
        stream.InitWriter();
        stream.Write(WinnerNumber);
        if (members != null)
        {
            stream.Write((byte)members.Length);
            foreach (LegacyNativeRoulettes.RouletteTable.Member member in members)
            {
                stream.Write(member.Betting);
                stream.Write(member.Winning);
                stream.Write(member.Owner.Player.Name, 32);
            }
        }
        else
        {
            stream.Write((byte)0);
        }
        stream.Finalize(2013);
        return stream;
    }

    public static LegacyNativePacket RouletteNoWinnerCreate(this LegacyNativePacket stream, byte Number)
    {
        stream.InitWriter();
        stream.Write(Number);
        stream.Finalize(2330);
        return stream;
    }

    public static LegacyNativePacket RouletteOpenGuiCreate(this LegacyNativePacket stream, LegacyNativeMsgRouletteTable.TableType Type, LegacyNativeMsgRouletteOpenGui.Color PlayerColor, byte TimerStamp, LegacyNativeRoulettes.RouletteTable.Member[]? Members = null)
    {
        stream.InitWriter();
        stream.Write((uint)Type);
        stream.Write((byte)PlayerColor);
        stream.Write(TimerStamp);
        if (Members != null)
        {
            stream.Write((byte)Members.Length);
            foreach (LegacyNativeRoulettes.RouletteTable.Member member in Members)
            {
                stream.Write(member.Owner.Player.UID);
                stream.Write(member.Owner.Player.Mesh);
                stream.Write((byte)member.Color);
                stream.Write(member.Owner.Player.Name, 32);
            }
        }
        else
        {
            stream.Write((byte)0);
        }
        stream.Finalize(2030);
        return stream;
    }

    public static void GetRouletteAction(this LegacyNativePacket stream, out LegacyNativeMsgRouletteAction.Action Mode)
    {
        Mode = (LegacyNativeMsgRouletteAction.Action)stream.ReadUInt8();
    }

    public static LegacyNativePacket RouletteScreenCreate(this LegacyNativePacket stream, uint UID)
    {
        stream.InitWriter();
        stream.Write((byte)0);
        stream.Write(UID);
        stream.Finalize(2112);
        return stream;
    }

    public static LegacyNativePacket RouletteShareBettingCreate(this LegacyNativePacket stream, LegacyNativeRoulettes.RouletteTable.Member player, LegacyNativeMsgRouletteOpenGui.Color color)
    {
        stream.InitWriter();
        stream.Write((byte)color);
        LegacyNativeMsgRouletteCheck.Item[] array = player.MyLuckNumber.Values.ToArray();
        LegacyNativeMsgRouletteCheck.Item[] array2 = player.MyLuckExtra.Values.ToArray();
        stream.Write((byte)(array.Length + array2.Length));
        LegacyNativeMsgRouletteCheck.Item[] array3 = array;
        foreach (LegacyNativeMsgRouletteCheck.Item item in array3)
        {
            stream.Write(item.Number);
            stream.Write(item.BetPrice);
        }
        LegacyNativeMsgRouletteCheck.Item[] array4 = array2;
        foreach (LegacyNativeMsgRouletteCheck.Item item2 in array4)
        {
            stream.Write(item2.Number);
            stream.Write(item2.BetPrice);
        }
        stream.Finalize(2090);
        return stream;
    }

    public static LegacyNativePacket RouletteSignUpCreate(this LegacyNativePacket stream, LegacyNativeMsgRouletteSignUp.ActionJoin Action, uint UID)
    {
        stream.InitWriter();
        stream.Write((byte)Action);
        stream.Write(UID);
        stream.Finalize(2048);
        return stream;
    }

    public static void GetRouletteSignUp(this LegacyNativePacket stream, out LegacyNativeMsgRouletteSignUp.ActionJoin Action, out uint UID)
    {
        Action = (LegacyNativeMsgRouletteSignUp.ActionJoin)stream.ReadUInt8();
        UID = stream.ReadUInt32();
    }

    public static LegacyNativePacket RouletteTableCreate(this LegacyNativePacket stream, uint UID, uint TableNumber, LegacyNativeMsgRouletteTable.TableType MoneyType, ushort X, ushort Y, ushort Mesh, byte PlayersCount)
    {
        stream.InitWriter();
        stream.Write(UID);
        stream.Write(TableNumber);
        stream.Write((uint)MoneyType);
        stream.Write(X);
        stream.Write(Y);
        stream.Write(Mesh);
        stream.Write(PlayersCount);
        stream.Finalize(2278);
        return stream;
    }
}
