namespace WolfConquer.Core.World;

// Retain the complete source threshold/rune calculation in CalculateUpdate.
// Public Update prepares a detached value before copying into this physical owner;
// preparation failure leaves its fields untouched. Concurrent-reader atomicity remains open.
internal partial class LegacyNativePerfectionEffect
{
	public void Update(int perfectionLevel, LegacyNativeGameClientRuntime client)
	{
		var prepared = PrepareUpdate(perfectionLevel, client);
		ApplyPrepared(prepared);
	}

	private void CalculateUpdate(int perfectionLevel, LegacyNativeGameClientRuntime client)
	{
		ToxinEraser = (LightOfStamina = (DivineGuard = (CalmWind = (LuckyStrike = (StrikeLock = (FreeSoul = (StraightLife = (DrainingTouch = (BloodSpawn = (CoreStrike = (KillingFlash = (MirrorOfSin = (InvisibleArrow = (ShieldBreak = (AbsoluteLuck = 0.0)))))))))))))));
		if (perfectionLevel > 0)
		{
			ToxinEraser = 30.0;
			LightOfStamina = 5.0;
			DivineGuard = 5.0;
		}
		if (perfectionLevel > 2)
		{
			CalmWind = 1.0;
		}
		if (perfectionLevel > 14)
		{
			LuckyStrike = 1.0;
		}
		if (perfectionLevel > 19)
		{
			StrikeLock = 1.0;
		}
		if (perfectionLevel > 24)
		{
			FreeSoul = 1.0;
		}
		if (perfectionLevel > 29)
		{
			StraightLife = 1.0;
		}
		if (perfectionLevel > 34)
		{
			DrainingTouch = 1.0;
		}
		if (perfectionLevel > 39)
		{
			BloodSpawn = 1.0;
		}
		if (perfectionLevel > 44)
		{
			ToxinEraser = 35.0;
		}
		if (perfectionLevel > 49)
		{
			CoreStrike = 1.0;
		}
		if (perfectionLevel > 54)
		{
			KillingFlash = 1.0;
		}
		if (perfectionLevel > 59)
		{
			MirrorOfSin = 1.0;
		}
		if (perfectionLevel > 64)
		{
			InvisibleArrow = 1.0;
		}
		if (perfectionLevel > 79)
		{
			CoreStrike = 2.0;
		}
		if (perfectionLevel > 84)
		{
			ToxinEraser = 40.0;
		}
		if (perfectionLevel > 89)
		{
			CalmWind = 2.0;
		}
		if (perfectionLevel > 94)
		{
			FreeSoul = 2.0;
		}
		if (perfectionLevel > 104)
		{
			StrikeLock = 2.0;
		}
		if (perfectionLevel > 109)
		{
			ToxinEraser = 45.0;
		}
		if (perfectionLevel > 114)
		{
			LuckyStrike = 2.0;
		}
		if (perfectionLevel > 124)
		{
			StraightLife = 2.0;
		}
		if (perfectionLevel > 129)
		{
			FreeSoul = 3.0;
		}
		if (perfectionLevel > 134)
		{
			CoreStrike = 3.0;
		}
		if (perfectionLevel > 144)
		{
			ToxinEraser = 50.0;
		}
		if (perfectionLevel > 154)
		{
			ShieldBreak = 1.0;
		}
		if (perfectionLevel > 164)
		{
			KillingFlash = 2.0;
		}
		if (perfectionLevel > 169)
		{
			CalmWind = 3.0;
		}
		if (perfectionLevel > 174)
		{
			StrikeLock = 3.0;
		}
		if (perfectionLevel > 184)
		{
			ToxinEraser = 60.0;
		}
		if (perfectionLevel > 189)
		{
			MirrorOfSin = 2.0;
		}
		if (perfectionLevel > 194)
		{
			FreeSoul = 4.0;
		}
		if (perfectionLevel > 199)
		{
			ToxinEraser = 70.0;
		}
		if (perfectionLevel > 204)
		{
			CoreStrike = 4.0;
		}
		if (perfectionLevel > 209)
		{
			StraightLife = 3.0;
		}
		if (perfectionLevel > 214)
		{
			LuckyStrike = 3.0;
		}
		if (perfectionLevel > 219)
		{
			DrainingTouch = 2.0;
		}
		if (perfectionLevel > 224)
		{
			InvisibleArrow = 2.0;
		}
		if (perfectionLevel > 229)
		{
			ToxinEraser = 80.0;
		}
		if (perfectionLevel > 239)
		{
			BloodSpawn = 2.0;
		}
		if (perfectionLevel > 244)
		{
			CoreStrike = 5.0;
		}
		if (perfectionLevel > 249)
		{
			CalmWind = 4.0;
		}
		if (perfectionLevel > 254)
		{
			ShieldBreak = 2.0;
		}
		if (perfectionLevel > 259)
		{
			ToxinEraser = 90.0;
		}
		if (perfectionLevel > 264)
		{
			StraightLife = 4.0;
		}
		if (perfectionLevel > 275)
		{
			DrainingTouch = 3.0;
		}
		if (perfectionLevel > 278)
		{
			FreeSoul = 5.0;
		}
		if (perfectionLevel > 281)
		{
			InvisibleArrow = 3.0;
		}
		if (perfectionLevel > 284)
		{
			CalmWind = 5.0;
		}
		if (perfectionLevel > 287)
		{
			BloodSpawn = 3.0;
		}
		if (perfectionLevel > 290)
		{
			MirrorOfSin = 3.0;
		}
		if (perfectionLevel > 293)
		{
			KillingFlash = 3.0;
		}
		if (perfectionLevel > 296)
		{
			ShieldBreak = 3.0;
		}
		if (perfectionLevel > 299)
		{
			ToxinEraser = 100.0;
		}
		if (perfectionLevel > 302)
		{
			StraightLife = 5.0;
		}
		if (perfectionLevel > 307)
		{
			StrikeLock = 4.0;
		}
		if (perfectionLevel > 311)
		{
			ShieldBreak = 4.0;
		}
		if (perfectionLevel > 313)
		{
			LuckyStrike = 4.0;
		}
		if (perfectionLevel > 319)
		{
			LuckyStrike = 5.0;
		}
		if (perfectionLevel > 320)
		{
			StrikeLock = 5.0;
		}
		if (perfectionLevel > 321)
		{
			ShieldBreak = 5.0;
		}
		if (perfectionLevel > 324)
		{
			AbsoluteLuck = 5.0;
		}
		if (perfectionLevel > 600)
		{
			ShieldBreak = 8.0;
			AbsoluteLuck = 7.0;
			StrikeLock = 7.0;
			LuckyStrike = 7.0;
			StraightLife = 9.0;
			ToxinEraser = 200.0;
			KillingFlash = 10.0;
			MirrorOfSin = 5.0;
			BloodSpawn = 5.0;
			CalmWind = 8.0;
			InvisibleArrow = 8.0;
			FreeSoul = 11.0;
			DrainingTouch = 10.0;
			StraightLife = 9.0;
			CoreStrike = 8.0;
			LightOfStamina = 8.0;
			DivineGuard = 8.0;
		}
		byte itemLevel = 0;
		if (client.Rune!.IsEquipped("Sanctity", ref itemLevel))
		{
			DivineGuard += (int)itemLevel;
		}
		if (client.Rune!.IsEquipped("FireBlast", ref itemLevel))
		{
			CoreStrike += (int)itemLevel;
		}
		if (client.Rune!.IsEquipped("Serenity", ref itemLevel))
		{
			InvisibleArrow += (int)itemLevel;
		}
		if (client.Rune!.IsEquipped("DrainingTouchBooster", ref itemLevel))
		{
			DrainingTouch += (int)itemLevel;
		}
		if (client.Rune!.IsEquipped("BloodSpawnBooster", ref itemLevel))
		{
			BloodSpawn += (int)itemLevel;
		}
		if (client.Rune!.IsEquipped("FreeSoulBooster", ref itemLevel))
		{
			FreeSoul += (int)itemLevel;
		}
		if (client.Rune!.IsEquipped("EnormousCrusher", ref itemLevel))
		{
			ShieldBreak += (int)itemLevel;
		}
	}
}
