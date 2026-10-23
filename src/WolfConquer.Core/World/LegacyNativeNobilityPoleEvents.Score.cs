using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgNobilityPole
{
    public static void FinishRound(LegacyNativePacket stream)
    {
        SortScores(getwinner: true);
        Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].Name = RoundOwner!.Name;
        LegacyNativeMsgSchedules.SendSysMesage("The [ King ]Round Ownered by " + RoundOwner!.Name + ".", LegacyNativeMsgMessage.ChatMode.Center);
        Reset(stream);
    }

    public static void UpdateScore(LegacyNativePacket stream, uint Score, LegacyNativePlayerRuntime Player)
    {
        if (!NobilityPoleList.ContainsKey(Player.UID))
        {
            NobilityPoleList.Add(Player.UID, new Info
            {
                Name = Player.Name,
                UID = Player.UID,
                Score = Score
            });
        }
        else
        {
            NobilityPoleList[Player.UID].Score += Score;
        }
        SortScores();
        if (Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].HitPoints < 1)
        {
            FinishRound(stream);
        }
    }

    private static void SortScores(bool getwinner = false)
    {
        if (Proces == LegacyNativeProcesType.Dead)
        {
            return;
        }
        Info[] source = NobilityPoleList.Values.ToArray();
        Info[] array = LegacyNativeArenaRankingOrder.Descending(source, (Info p) => p.Score);
        for (int num = 0; num < array.Length; num++)
        {
            Info info = array[num];
            if (num == 0 && getwinner)
            {
                RoundOwner = info;
            }
            using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
            {
                LegacyNativeMsgMessage msgMessage = new LegacyNativeMsgMessage("No " + (num + 1) + ". " + info.Name + " (" + info.Score + ")", LegacyNativeMsgMessage.MsgColor.yellow, (num == 0) ? LegacyNativeMsgMessage.ChatMode.FirstRightCorner : LegacyNativeMsgMessage.ChatMode.ContinueRightCorner);
                SendMapPacket(msgMessage.GetArray(recycledPacket.GetStream()));
            }
            if (num == 4)
            {
                break;
            }
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole1
{
    public static void FinishRound(LegacyNativePacket stream)
    {
        SortScores(getwinner: true);
        Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].Name = RoundOwner!.Name;
        LegacyNativeMsgSchedules.SendSysMesage("The [ Prince ]Round Ownered by " + RoundOwner!.Name + ".", LegacyNativeMsgMessage.ChatMode.Center);
        Reset(stream);
    }

    public static void UpdateScore(LegacyNativePacket stream, uint Score, LegacyNativePlayerRuntime Player)
    {
        if (!NobilityPoleList.ContainsKey(Player.UID))
        {
            NobilityPoleList.Add(Player.UID, new Info
            {
                Name = Player.Name,
                UID = Player.UID,
                Score = Score
            });
        }
        else
        {
            NobilityPoleList[Player.UID].Score += Score;
        }
        SortScores();
        if (Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].HitPoints < 1)
        {
            FinishRound(stream);
        }
    }

    private static void SortScores(bool getwinner = false)
    {
        if (Proces == LegacyNativeProcesType.Dead)
        {
            return;
        }
        Info[] source = NobilityPoleList.Values.ToArray();
        Info[] array = LegacyNativeArenaRankingOrder.Descending(source, (Info p) => p.Score);
        for (int num = 0; num < array.Length; num++)
        {
            Info info = array[num];
            if (num == 0 && getwinner)
            {
                RoundOwner = info;
            }
            using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
            {
                LegacyNativeMsgMessage msgMessage = new LegacyNativeMsgMessage("No " + (num + 1) + ". " + info.Name + " (" + info.Score + ")", LegacyNativeMsgMessage.MsgColor.yellow, (num == 0) ? LegacyNativeMsgMessage.ChatMode.FirstRightCorner : LegacyNativeMsgMessage.ChatMode.ContinueRightCorner);
                SendMapPacket(msgMessage.GetArray(recycledPacket.GetStream()));
            }
            if (num == 4)
            {
                break;
            }
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole2
{
    public static void FinishRound(LegacyNativePacket stream)
    {
        SortScores(getwinner: true);
        Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].Name = RoundOwner!.Name;
        LegacyNativeMsgSchedules.SendSysMesage("The [ Duke ]Round Ownered by " + RoundOwner!.Name + ".", LegacyNativeMsgMessage.ChatMode.Center);
        Reset(stream);
    }

    public static void UpdateScore(LegacyNativePacket stream, uint Score, LegacyNativePlayerRuntime Player)
    {
        if (!NobilityPoleList.ContainsKey(Player.UID))
        {
            NobilityPoleList.Add(Player.UID, new Info
            {
                Name = Player.Name,
                UID = Player.UID,
                Score = Score
            });
        }
        else
        {
            NobilityPoleList[Player.UID].Score += Score;
        }
        SortScores();
        if (Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].HitPoints < 1)
        {
            FinishRound(stream);
        }
    }

    private static void SortScores(bool getwinner = false)
    {
        if (Proces == LegacyNativeProcesType.Dead)
        {
            return;
        }
        Info[] source = NobilityPoleList.Values.ToArray();
        Info[] array = LegacyNativeArenaRankingOrder.Descending(source, (Info p) => p.Score);
        for (int num = 0; num < array.Length; num++)
        {
            Info info = array[num];
            if (num == 0 && getwinner)
            {
                RoundOwner = info;
            }
            using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
            {
                LegacyNativeMsgMessage msgMessage = new LegacyNativeMsgMessage("No " + (num + 1) + ". " + info.Name + " (" + info.Score + ")", LegacyNativeMsgMessage.MsgColor.yellow, (num == 0) ? LegacyNativeMsgMessage.ChatMode.FirstRightCorner : LegacyNativeMsgMessage.ChatMode.ContinueRightCorner);
                SendMapPacket(msgMessage.GetArray(recycledPacket.GetStream()));
            }
            if (num == 4)
            {
                break;
            }
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole3
{
    public static void FinishRound(LegacyNativePacket stream)
    {
        SortScores(getwinner: true);
        Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].Name = RoundOwner!.Name;
        LegacyNativeMsgSchedules.SendSysMesage("The [ Earl ] Round Ownered by " + RoundOwner!.Name + ".", LegacyNativeMsgMessage.ChatMode.Center);
        Reset(stream);
    }

    public static void UpdateScore(LegacyNativePacket stream, uint Score, LegacyNativePlayerRuntime Player)
    {
        if (!NobilityPoleList.ContainsKey(Player.UID))
        {
            NobilityPoleList.Add(Player.UID, new Info
            {
                Name = Player.Name,
                UID = Player.UID,
                Score = Score
            });
        }
        else
        {
            NobilityPoleList[Player.UID].Score += Score;
        }
        SortScores();
        if (Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].HitPoints < 1)
        {
            FinishRound(stream);
        }
    }

    private static void SortScores(bool getwinner = false)
    {
        if (Proces == LegacyNativeProcesType.Dead)
        {
            return;
        }
        Info[] source = NobilityPoleList.Values.ToArray();
        Info[] array = LegacyNativeArenaRankingOrder.Descending(source, (Info p) => p.Score);
        for (int num = 0; num < array.Length; num++)
        {
            Info info = array[num];
            if (num == 0 && getwinner)
            {
                RoundOwner = info;
            }
            using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
            {
                LegacyNativeMsgMessage msgMessage = new LegacyNativeMsgMessage("No " + (num + 1) + ". " + info.Name + " (" + info.Score + ")", LegacyNativeMsgMessage.MsgColor.yellow, (num == 0) ? LegacyNativeMsgMessage.ChatMode.FirstRightCorner : LegacyNativeMsgMessage.ChatMode.ContinueRightCorner);
                SendMapPacket(msgMessage.GetArray(recycledPacket.GetStream()));
            }
            if (num == 4)
            {
                break;
            }
        }
    }
}
