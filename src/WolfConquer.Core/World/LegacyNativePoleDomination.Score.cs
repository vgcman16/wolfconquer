using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativePoleDomination
{
    // Entire native144..166; lethal-pole path invokes the real full round.
    internal void UpdateScore(LegacyNativePlayerRuntime client, uint Damage)
    {
        if (client.MyGuild != null && Proces == LegacyNativeProcesType.Alive)
        {
            if (!ScoreList.ContainsKey(client.GuildID))
            {
                ScoreList.TryAdd(client.GuildID, new PoleDomination
                {
                    GuildID = client.MyGuild.Info.GuildID,
                    Name = client.MyGuild.GuildName,
                    Score = Damage
                });
            }
            else
            {
                ScoreList[client.MyGuild.Info.GuildID].Score += Damage;
            }
            if (Furnitures[LegacyNativeSobNpcRuntime.StaticMesh.Pole].HitPoints == 0)
            {
                FinishRound();
            }
        }
    }

    // Entire native168..195. Winner is the original score-row alias, not a copy.
    internal void ShuffleGuildScores(bool createWinned = false)
    {
        if (Proces == LegacyNativeProcesType.Dead)
        {
            return;
        }
        StampShuffleScore = DateTime.Now.AddSeconds(5.0);
        PoleDomination[] source = ScoreList.Values.ToArray();
        // Framework reads every uint key before publication, including a null singleton.
        PoleDomination[] array = LegacyNativeArenaRankingOrder.Descending(source, (PoleDomination p) => p.Score);
        for (int num = 0; num < array.Length; num++)
        {
            PoleDomination poleDomination = array[num];
            if (num == 0 && createWinned)
            {
                Winner = poleDomination;
            }
            using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
            {
                LegacyNativePacket stream = recycledPacket.GetStream();
                LegacyNativeMsgMessage msgMessage = new LegacyNativeMsgMessage(
                    "No " + (num + 1) + ". " + poleDomination.Name + " (" + poleDomination.Score + ")",
                    LegacyNativeMsgMessage.MsgColor.yellow,
                    (num == 0) ? LegacyNativeMsgMessage.ChatMode.FirstRightCorner : LegacyNativeMsgMessage.ChatMode.ContinueRightCorner);
                SendMapPacket(msgMessage.GetArray(stream));
            }
            if (num == 4)
            {
                break;
            }
        }
    }
}
