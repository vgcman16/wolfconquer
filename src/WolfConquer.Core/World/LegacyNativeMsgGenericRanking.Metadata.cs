using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Native metadata only; full Process2165 gameplay dispatch remains absent.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgGenericRanking
{
	public enum Action : uint
	{
		Ranking = 1u,
		QueryCount = 2u,
		UpdateScreen = 4u,
		InformationRequest = 5u,
		PrestigeRanks = 6u
	}

	public enum RankType : uint
	{
		None = 0u,
		RoseFairy = 30000002u,
		LilyFairy = 30000102u,
		OrchidFairy = 30000202u,
		TulipFairy = 30000302u,
		Kiss = 30000402u,
		Love = 30000502u,
		Tins = 30000602u,
		Jade = 30000702u,
		Chi = 60000000u,
		DragonChi = 60000001u,
		PhoenixChi = 60000002u,
		TigerChi = 60000003u,
		TurtleChi = 60000004u,
		InnerPower = 70000000u,
		PrestigeRank = 80000000u,
		TopTrojans = 80000001u,
		TopWarriors = 80000002u,
		TopArchers = 80000003u,
		TopNinjas = 80000004u,
		TopMonks = 80000005u,
		TopPirates = 80000006u,
		TopDraonWarriors = 80000007u,
		TopWaters = 80000008u,
		TopFires = 80000009u,
		TopWindWalker = 80000010u,
		TopThunderstriker = 80000011u,
		Rune = 110000000u,
		Overall = 130000000u,
		Fire = 130000001u,
		Water = 130000002u,
		Earth = 130000003u,
		Wind = 130000004u,
		Lightning = 130000005u,
		HundredWeaponBegining = 120000000u,
		HundredWeaponEnding = 120000010u,
		Overall_Warrior = 150000000u,
		Dragonhowl = 150000001u,
		Bloodlust = 150000002u,
		Redcurse = 150000003u
	}

	public static object SynRoot = new object();
}
