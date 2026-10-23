using System;
using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMobItemGenerator
{
	private static ushort[] NecklaceType = new ushort[2] { 120, 121 };

	private static ushort[] RingType = new ushort[2] { 150, 151 };

	private static ushort[] ArmetType = new ushort[6] { 111, 112, 113, 114, 117, 118 };

	private static ushort[] ArmorType = new ushort[5] { 130, 131, 132, 133, 134 };

	private static ushort[] OneHanderType = new ushort[12]
	{
		410, 420, 421, 430, 440, 450, 460, 480, 481, 490,
		500, 601
	};

	private static ushort[] TwoHanderType = new ushort[6] { 510, 530, 560, 561, 580, 900 };

	private static uint[] SeaPotions = new uint[9] { 3004230u, 3004231u, 3004232u, 3004233u, 3004234u, 3004235u, 3004236u, 3004237u, 3004238u };

	private LegacyNativeMonsterFamily Family;

	private LegacyNativeMobRateWatcher Refined;

	private LegacyNativeMobRateWatcher Unique;

	private LegacyNativeMobRateWatcher Elite;

	private LegacyNativeMobRateWatcher Super;

	private LegacyNativeMobRateWatcher PlusOne;

	private LegacyNativeMobRateWatcher PlusTwo;

	private LegacyNativeMobRateWatcher DropHp;

	private LegacyNativeMobRateWatcher DropMp;

	private LegacyNativeMobRateWatcher Chi100;

	private LegacyNativeMobRateWatcher Study20;

	private LegacyNativeMobRateWatcher Chi300;

	private LegacyNativeMobRateWatcher Bomb;

	private LegacyNativeMobRateWatcher LuckyAmulet;

	private LegacyNativeMobRateWatcher MoonBox;

	private LegacyNativeMobRateWatcher DropSpecialPotions;

	private LegacyNativeMobRateWatcher CuteCPPack;

	private LegacyNativeMobRateWatcher DragonBalls;

	private LegacyNativeMobRateWatcher Letter14;

	private LegacyNativeMobRateWatcher LetterC;

	private LegacyNativeMobRateWatcher LetterO;

	private LegacyNativeMobRateWatcher LetterN;

	private LegacyNativeMobRateWatcher LetterQ;

	private LegacyNativeMobRateWatcher LetterU;

	private LegacyNativeMobRateWatcher LetterE;

	private LegacyNativeMobRateWatcher LetterR;

	public LegacyNativeMobItemGenerator(LegacyNativeMonsterFamily family)
	{
		Family = family;
		Refined = new LegacyNativeMobRateWatcher(10);
		Unique = new LegacyNativeMobRateWatcher(20);
		Elite = new LegacyNativeMobRateWatcher(25);
		Super = new LegacyNativeMobRateWatcher(30);
		PlusOne = new LegacyNativeMobRateWatcher(20);
		PlusTwo = new LegacyNativeMobRateWatcher(30);
		DropHp = new LegacyNativeMobRateWatcher(50);
		DropMp = new LegacyNativeMobRateWatcher(50);
		DropSpecialPotions = new LegacyNativeMobRateWatcher(50);
		LuckyAmulet = new LegacyNativeMobRateWatcher(300);
		Letter14 = new LegacyNativeMobRateWatcher(400);
		LetterC = new LegacyNativeMobRateWatcher(200);
		LetterO = new LegacyNativeMobRateWatcher(200);
		LetterN = new LegacyNativeMobRateWatcher(200);
		LetterQ = new LegacyNativeMobRateWatcher(200);
		LetterU = new LegacyNativeMobRateWatcher(200);
		LetterE = new LegacyNativeMobRateWatcher(200);
		LetterR = new LegacyNativeMobRateWatcher(200);
		Chi100 = new LegacyNativeMobRateWatcher(100);
		Chi300 = new LegacyNativeMobRateWatcher(200);
		MoonBox = new LegacyNativeMobRateWatcher(700);
		Study20 = new LegacyNativeMobRateWatcher(70);
		Bomb = new LegacyNativeMobRateWatcher(300);
		CuteCPPack = new LegacyNativeMobRateWatcher(140);
		DragonBalls = new LegacyNativeMobRateWatcher(150);
	}


}
