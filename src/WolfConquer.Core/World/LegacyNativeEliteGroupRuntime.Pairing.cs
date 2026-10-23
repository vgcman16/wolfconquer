using WolfConquer.Core.Data;
namespace WolfConquer.Core.World;
internal partial class LegacyNativeEliteGroupRuntime
{
public void CreateDoubleMatchs(LegacyNativeDbSafeDictionary<uint, Match> Array)
	{
		LegacyNativeGameClientRuntime[] values = Players.GetValues();
		foreach (LegacyNativeGameClientRuntime user in values)
		{
			Match doubleImcompleteMatch = GetDoubleImcompleteMatch(Array);
			doubleImcompleteMatch.AddPlayer(user);
			if (!Array.ContainsKey(doubleImcompleteMatch.ID))
			{
				Array.Add(doubleImcompleteMatch.ID, doubleImcompleteMatch);
			}
		}
	}

public Match GetDoubleImcompleteMatch(LegacyNativeDbSafeDictionary<uint, Match> Array)
	{
		Match[] values = Array.GetValues();
		foreach (Match match in values)
		{
			if (match.Count < 2)
			{
				return match;
			}
		}
		Match match2 = new Match(this);
		match2.Index = (ushort)MatchIndex++;
		match2.ID = MatchCounter.Next;
		return match2;
	}

public void CreateTripleMatchs(LegacyNativeDbSafeDictionary<uint, Match> Array)
	{
		LegacyNativeGameClientRuntime[] array = Players.Values.ToArray();
		if (array.Length <= 16)
		{
			ushort num = 0;
			int num2 = array.Length - 8;
			for (int i = 0; i < num2; i++)
			{
				try
				{
					Match match = new Match(this);
					match.Index = (ushort)MatchIndex++;
					match.ID = MatchCounter.Next;
					match.AddPlayer(array[num++]);
					match.AddPlayer(array[num++]);
					Array.Add(match.ID, match);
				}
				catch
				{
					num++;
				}
			}
			for (int j = 0; j < 8 - num2; j++)
			{
				try
				{
					Match match2 = new Match(this);
					match2.Index = (ushort)MatchIndex++;
					match2.ID = MatchCounter.Next;
					match2.AddPlayer(array[num++], FighterStats.StatusFlag.Qualified);
					Array.Add(match2.ID, match2);
				}
				catch
				{
					num++;
				}
			}
			return;
		}
		ushort num3 = 0;
		int num4 = array.Length - 16;
		for (int k = 0; k < num4; k++)
		{
			int num5 = num3++;
			int num6 = num3++;
			int num7 = num3++;
			try
			{
				Match match3 = new Match(this);
				match3.Index = (ushort)MatchIndex++;
				match3.ID = MatchCounter.Next;
				match3.AddPlayer(array[num5]);
				match3.AddPlayer(array[num6]);
				match3.AddPlayer(array[num7]);
				match3.AddWaiting();
				Array.Add(match3.ID, match3);
			}
			catch
			{
			}
		}
		int num8 = array.Length - num3;
		for (int l = 0; l < num8 / 2; l++)
		{
			int num9 = num3++;
			int num10 = num3++;
			try
			{
				Match match4 = new Match(this);
				match4.Index = (ushort)MatchIndex++;
				match4.ID = MatchCounter.Next;
				match4.AddPlayer(array[num9]);
				match4.AddPlayer(array[num10]);
				Array.Add(match4.ID, match4);
			}
			catch
			{
			}
		}
	}

public bool Complete8Match()
	{
		return Matches!.Count < 8;
	}

public Match[] ArrayMatchesTop3()
	{
		Match[] array = new Match[((Top4Matches != null) ? Top4Matches.Count : 0) + ((ThreeQualiferMatch != null) ? ThreeQualiferMatch.Count : 0) + ((FinalMatch != null) ? FinalMatch.Count : 0)];
		int num = -1;
		for (int i = 0; i < ((Top4Matches != null) ? Top4Matches.Count : 0); i++)
		{
			array[++num] = Top4Matches!.GetValues()[i];
		}
		for (int j = 0; j < ((ThreeQualiferMatch != null) ? ThreeQualiferMatch.Count : 0); j++)
		{
			array[++num] = ThreeQualiferMatch!.GetValues()[j];
		}
		for (int k = 0; k < ((FinalMatch != null) ? FinalMatch.Count : 0); k++)
		{
			array[++num] = FinalMatch!.GetValues()[k];
		}
		return array;
	}
}
