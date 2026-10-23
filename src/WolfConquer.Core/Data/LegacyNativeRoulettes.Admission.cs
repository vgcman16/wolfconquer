using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using WolfConquer.Core.World;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRoulettes
{
    public partial class RouletteTable
    {
        public void AddWatch(LegacyNativePacket stream, LegacyNativeGameClientRuntime client)
        {
            if (RegistredPlayers.Count == 0)
            {
                client.SendSysMesage("Failed to spectate. No one is at the table.", LegacyNativeMsgMessage.ChatMode.Talk);
            }
            else if (client.PlayRouletteUID != 0)
            {
                client.SendSysMesage("You are play another table. Please quit before you spectate this one.", LegacyNativeMsgMessage.ChatMode.Talk);
            }
            else if (client.WatchRoulette != 0)
            {
                client.SendSysMesage("You are spectating another table. Please quit before you spectate this one.", LegacyNativeMsgMessage.ChatMode.Talk);
            }
            else if (!ClientsWatch.ContainsKey(client.Player.UID))
            {
                client.WatchRoulette = SpawnPacket.UID;
                ClientsWatch.TryAdd(client.Player.UID, client);
                SendPacketTable(stream.RouletteScreenCreate(client.Player.UID));
                client.Send(stream.RouletteOpenGuiCreate(SpawnPacket.MoneyType, LegacyNativeMsgRouletteOpenGui.Color.Watch, (byte)GetTimerStamp, RegistredPlayers.Values.Where((Member p) => p.Owner.Player.UID != client.Player.UID).ToArray()));
            }
        }

        public void RemoveWatch(uint UID)
        {
            if (ClientsWatch.TryRemove(UID, out var value))
            {
                value.WatchRoulette = 0u;
                using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
                LegacyNativePacket stream = recycledPacket.GetStream();
                value.Send(stream.RouletteSignUpCreate(LegacyNativeMsgRouletteSignUp.ActionJoin.Quit, value.Player.UID));
            }
        }

        public void AddPlayer(LegacyNativePacket stream, LegacyNativeGameClientRuntime client)
        {
            if ((long)RegistredPlayers.Count >= 5L)
            {
                client.SendSysMesage("Sorry, this game table is full.", LegacyNativeMsgMessage.ChatMode.Talk);
            }
            else if (client.PlayRouletteUID != 0)
            {
                client.SendSysMesage("You are play another table. Please quit before you playing this one.", LegacyNativeMsgMessage.ChatMode.Talk);
            }
            else
            {
                if (RegistredPlayers.ContainsKey(client.Player.UID))
                {
                    return;
                }
                client.PlayRouletteUID = SpawnPacket.UID;
                Member member = new Member();
                member.Owner = client;
                GeneratePlayerColor(out member.Color);
                RegistredPlayers.TryAdd(member.Owner.Player.UID, member);
                ApplyTime(stream, member);
                if (RegistredPlayers.Count <= 1)
                {
                    return;
                }
                ShareTableBetting(stream, client);
                foreach (Member value in RegistredPlayers.Values)
                {
                    if (value.Owner.Player.UID != client.Player.UID)
                    {
                        value.Owner.Send(stream.RoulettedAddNewPlayerCreate(client.Player.UID, client.Player.Mesh, member.Color, client.Player.Name));
                    }
                }
                foreach (LegacyNativeGameClientRuntime value2 in ClientsWatch.Values)
                {
                    value2.Send(stream.RoulettedAddNewPlayerCreate(client.Player.UID, client.Player.Mesh, member.Color, client.Player.Name));
                }
            }
        }

        public void ShareTableBetting(LegacyNativePacket stream, LegacyNativeGameClientRuntime client)
        {
            foreach (Member value in RegistredPlayers.Values)
            {
                if (value.MyLuckExtra.Count != 0 || value.MyLuckNumber.Count != 0)
                {
                    client.Send(stream.RouletteShareBettingCreate(value, value.Color));
                }
            }
        }

        public void RemovePlayer(LegacyNativeGameClientRuntime client)
        {
            using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
            {
                LegacyNativePacket stream = recycledPacket.GetStream();
                SendPacketTable(stream.RouletteSignUpCreate(LegacyNativeMsgRouletteSignUp.ActionJoin.Quit, client.Player.UID));
            }
            if (!RegistredPlayers.TryRemove(client.Player.UID, out var _))
            {
                return;
            }
            client.PlayRouletteUID = 0u;
            if (RegistredPlayers.Count != 0)
            {
                return;
            }
            foreach (LegacyNativeGameClientRuntime value2 in ClientsWatch.Values)
            {
                RemoveWatch(value2.Player.UID);
            }
        }

        public void SendPacketTable(LegacyNativePacket packet)
        {
            foreach (Member value in RegistredPlayers.Values)
            {
                value.Owner.Send(packet);
            }
            foreach (LegacyNativeGameClientRuntime value2 in ClientsWatch.Values)
            {
                value2.Send(packet);
            }
        }

        public bool ContainColor(LegacyNativeMsgRouletteOpenGui.Color color)
        {
            foreach (Member value in RegistredPlayers.Values)
            {
                if (value.Color == color)
                {
                    return true;
                }
            }
            return false;
        }

        public void GeneratePlayerColor(out LegacyNativeMsgRouletteOpenGui.Color color)
        {
            color = LegacyNativeMsgRouletteOpenGui.Color.None;
            for (int i = 0; i < 5; i++)
            {
                if (!ContainColor((LegacyNativeMsgRouletteOpenGui.Color)i))
                {
                    color = (LegacyNativeMsgRouletteOpenGui.Color)i;
                    break;
                }
            }
        }

        public void ApplyTime(LegacyNativePacket stream, Member player)
        {
            if (RegistredPlayers.Count == 1)
            {
                player.Owner.Send(stream.RouletteOpenGuiCreate(SpawnPacket.MoneyType, player.Color, (byte)GetTimerStamp, new Member[0]));
                return;
            }
            player.Owner.Send(stream.RouletteOpenGuiCreate(SpawnPacket.MoneyType, player.Color, (byte)GetTimerStamp, RegistredPlayers.Values.Where((Member p) => p.Owner.Player.UID != player.Owner.Player.UID).ToArray()));
        }

        public void ApplayNumberWinner(LegacyNativeGameClientRuntime client)
        {
            using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
            LegacyNativePacket stream = recycledPacket.GetStream();
            client.Send(stream.RouletteNoWinnerCreate(LuckyNumber));
        }
    }
}
