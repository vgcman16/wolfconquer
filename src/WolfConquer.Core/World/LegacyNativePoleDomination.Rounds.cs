using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativePoleDomination
{
    // Whole native99..118, including genuine global announcement and post-send cleanup.
    internal void CompletePoleDomination()
    {
        SendInvitation = false;
        ShuffleGuildScores();
        Proces = LegacyNativeProcesType.Dead;
        ScoreList.Clear();
        using (new LegacyNativeRecycledPacket())
        {
            if (Winner.Name != "None" && Winner.Score != 100)
            {
                LegacyNativeMsgSchedules.SendSysMesage(
                    "Congratulations to " + Winner.Name + ", they've won the  First PoleDomination  with a score of " + Winner.Score,
                    LegacyNativeMsgMessage.ChatMode.Center);
            }
            else
            {
                LegacyNativeMsgSchedules.SendSysMesage("First PoleDomination Has Endd Now  with no winner.",
                    LegacyNativeMsgMessage.ChatMode.Center);
            }
        }
        RewardLeader.Clear();
        Winner.LeaderReward = 1;
    }

    // Whole native120..128; partial state on reset faults is source-observable.
    internal void Start()
    {
        Proces = LegacyNativeProcesType.Alive;
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        ResetFurnitures(stream);
        ScoreList.Clear();
        LegacyNativeMsgSchedules.SendSysMesage("First PoleDomination war in Skay City has started!.",
            LegacyNativeMsgMessage.ChatMode.Center);
    }

    // Whole native130..142; source does not change Proces or rewards here.
    internal void FinishRound()
    {
        ShuffleGuildScores(createWinned: true);
        Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].Name = Winner.Name;
        ScoreList.Clear();
        using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
        {
            LegacyNativeMsgSchedules.SendSysMesage(
                "Congratulations to " + Winner.Name + ", they've won the First PoleDomination round with a score of " + Winner.Score,
                LegacyNativeMsgMessage.ChatMode.Center);
            LegacyNativePacket stream = recycledPacket.GetStream();
            ResetFurnitures(stream);
        }
        StampRound = DateTime.Now.AddSeconds(3.0);
    }
}
