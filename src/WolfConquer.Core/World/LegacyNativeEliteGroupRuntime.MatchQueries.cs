using WolfConquer.Core.Data;
namespace WolfConquer.Core.World;

internal partial class LegacyNativeEliteGroupRuntime
{
    public partial class Match
    {
public FighterStats[] GetMatchStats
		{
			get
			{
				if (elitepkgroup!.State == LegacyNativeMsgElitePKBrackets.GuiTyp.GUI_Top8Qualifier)
				{
					FighterStats[] array = new FighterStats[PlayersArray.Length];
					for (int i = 0; i < array.Length; i++)
					{
						array[i] = PlayersArray[i].ElitePKStats!;
					}
					return array;
				}
				return MatchStats.GetValues();
			}
		}

public Match AddPlayer(LegacyNativeGameClientRuntime user, FighterStats.StatusFlag flag = FighterStats.StatusFlag.None)
		{
			Players.Add(user.Player.UID, user);
			user.ElitePkMatch = this;
			user.ElitePKStats = new FighterStats(user.Player.UID, user.Player.Name, user.Player.Mesh, user.Player.ServerID, user.Player.RealUID);
			user.ElitePKStats!.Flag = flag;
			MatchStats.Add(user.Player.UID, user.ElitePKStats!);
			return this;
		}

public void AddWaiting()
		{
			if (Count == 3)
			{
				PlayerWaiting = PlayersArray[0];
				PlayerWaiting!.ElitePKStats!.Flag = FighterStats.StatusFlag.Waiting;
			}
		}

public void CheckFinish()
		{
			if (Count == 1)
			{
				Done = (Exported = true);
				Flag = StatusFlag.OK;
				LegacyNativeGameClientRuntime[] playersArray = PlayersArray;
				foreach (LegacyNativeGameClientRuntime gameClient in playersArray)
				{
					gameClient.ElitePKStats!.Flag = FighterStats.StatusFlag.Qualified;
				}
			}
		}

public LegacyNativeGameClientRuntime? GetOpponent(uint UID)
		{
			if (Count == 2)
			{
				LegacyNativeGameClientRuntime[] playersArray = PlayersArray;
				foreach (LegacyNativeGameClientRuntime gameClient in playersArray)
				{
					if (gameClient.Player.UID != UID)
					{
						return gameClient;
					}
				}
			}
			else if (Count == 3)
			{
				if (PlayerWaiting!.ElitePKStats!.Flag == FighterStats.StatusFlag.Lost)
				{
					LegacyNativeGameClientRuntime[] playersArray2 = PlayersArray;
					foreach (LegacyNativeGameClientRuntime gameClient2 in playersArray2)
					{
						if (gameClient2.Player.UID != UID && PlayerWaiting!.ElitePKStats!.UID != gameClient2.ElitePKStats!.UID)
						{
							return gameClient2;
						}
					}
				}
				if (Flag != StatusFlag.SwitchOut)
				{
					LegacyNativeGameClientRuntime[] playersArray3 = PlayersArray;
					foreach (LegacyNativeGameClientRuntime gameClient3 in playersArray3)
					{
						if (gameClient3.Player.UID != UID && gameClient3.ElitePKStats!.Flag != FighterStats.StatusFlag.Waiting)
						{
							return gameClient3;
						}
					}
				}
				else if (Flag == StatusFlag.SwitchOut)
				{
					LegacyNativeGameClientRuntime[] playersArray4 = PlayersArray;
					foreach (LegacyNativeGameClientRuntime gameClient4 in playersArray4)
					{
						if (gameClient4.Player.UID != UID && gameClient4.ElitePKStats!.Flag != FighterStats.StatusFlag.Lost)
						{
							return gameClient4;
						}
					}
				}
			}
			return null;
		}

public void Switch()
		{
			if (Count == 3)
			{
				if (PlayerWaiting!.ElitePKStats!.Flag == FighterStats.StatusFlag.Lost)
				{
					Flag = StatusFlag.OK;
					return;
				}
				if ((from p in PlayersArray
					where p.Player.UID != PlayerWaiting!.Player.UID
					where p.ElitePKStats!.Flag == FighterStats.StatusFlag.Lost
					select p).ToArray().Length == 2)
				{
					Flag = StatusFlag.OK;
					return;
				}
				Imported = false;
				Done = false;
				Exported = false;
				Flag = StatusFlag.SwitchOut;
				PlayerWaiting!.ElitePkMatch = this;
				PlayerWaiting!.ElitePKStats = new FighterStats(PlayerWaiting!.Player.UID, PlayerWaiting!.Player.Name, PlayerWaiting!.Player.Mesh, PlayerWaiting!.Player.ServerID, PlayerWaiting!.Player.RealUID);
			}
		}
    }
}
