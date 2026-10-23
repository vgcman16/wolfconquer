using System;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMonsterRole
{
    // Entire native MonsterRole238..282; no lock/zero-damage guard is added.
    public void UpdateScore(LegacyNativePacket stream, LegacyNativeGameClientRuntime killer, uint Damage)
    {
        try
        {
            if (Hunters == null)
            {
                Hunters = new Dictionary<uint, LegacyNativeMsgBossHarmRankingEntry>();
            }
            if (Hunters.ContainsKey(killer.Player.UID))
            {
                Hunters[killer.Player.UID].HunterScore += Damage;
            }
            else if (!Hunters.ContainsKey(killer.Player.UID))
            {
                Hunters.Add(killer.Player.UID, new LegacyNativeMsgBossHarmRankingEntry
                {
                    HunterUID = killer.Player.UID,
                    HunterScore = Damage,
                    Rank = 0u,
                    HunterName = killer.Player.Name,
                    ServerID = killer.Player.ServerID
                });
            }
            if (Hunters.Count != 0)
            {
                LegacyNativeMsgBossHarmRankingEntry[] array = Hunters.Values.OrderByDescending((LegacyNativeMsgBossHarmRankingEntry p) => p.HunterScore).ToArray();
                for (int num = 0; num < array.Length; num++)
                {
                    array[num].Rank = (uint)(num + 1);
                }
                Send(stream.CreateBossHarmRankList(new LegacyNativeMsgBossHarmRanking
                {
                    MonsterID = Family.ID,
                    Type = LegacyNativeMsgBossHarmRanking.RankAction.ShowRespondForTheRest,
                    Hunters = array
                }));
                array = null!;
            }
        }
        catch (Exception e)
        {
            LegacyNativeConsole.WriteException(e);
            LegacyNativeConsole.SaveException(e);
        }
    }
}
