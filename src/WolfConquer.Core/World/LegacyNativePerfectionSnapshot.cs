namespace WolfConquer.Core.World;

// Source GameClient.PerfectionStatus is distinct from MsgStatus scalar fields and persisted inputs.
internal sealed record LegacyNativePerfectionSnapshot(
    double ToxinEraser = 0, double LightOfStamina = 0, double DivineGuard = 0,
    double CalmWind = 0, double LuckyStrike = 0, double StrikeLock = 0,
    double FreeSoul = 0, double StraightLife = 0, double DrainingTouch = 0,
    double BloodSpawn = 0, double CoreStrike = 0, double KillingFlash = 0,
    double MirrorOfSin = 0, double InvisibleArrow = 0, double ShieldBreak = 0,
    double AbsoluteLuck = 0)
{
    internal static readonly LegacyNativePerfectionSnapshot Empty = new();

    internal static LegacyNativePerfectionSnapshot FromPrestige(uint raw, Func<string, byte> rune)
    {
        var level = unchecked((int)raw); // Native CreatePrestigePoints passes this exact conversion.
        // Native Update performs these eligible-rune reads in this order, after all thresholds.
        var sanctity = rune("Sanctity");
        var fireBlast = rune("FireBlast");
        var serenity = rune("Serenity");
        var draining = rune("DrainingTouchBooster");
        var blood = rune("BloodSpawnBooster");
        var freeSoul = rune("FreeSoulBooster");
        var enormous = rune("EnormousCrusher");
        return new(
            ToxinEraser: level switch
            {
                > 600 => 200, > 299 => 100, > 259 => 90, > 229 => 80, > 199 => 70,
                > 184 => 60, > 144 => 50, > 109 => 45, > 84 => 40, > 44 => 35, > 0 => 30, _ => 0,
            },
            LightOfStamina: level > 600 ? 8 : level > 0 ? 5 : 0,
            DivineGuard: (level > 600 ? 8 : level > 0 ? 5 : 0) + sanctity,
            CalmWind: level switch { > 600 => 8, > 284 => 5, > 249 => 4, > 169 => 3, > 89 => 2, > 2 => 1, _ => 0 },
            LuckyStrike: level switch { > 600 => 7, > 319 => 5, > 313 => 4, > 214 => 3, > 114 => 2, > 14 => 1, _ => 0 },
            StrikeLock: level switch { > 600 => 7, > 320 => 5, > 307 => 4, > 174 => 3, > 104 => 2, > 19 => 1, _ => 0 },
            FreeSoul: (level switch { > 600 => 11, > 278 => 5, > 194 => 4, > 129 => 3, > 94 => 2, > 24 => 1, _ => 0 }) + freeSoul,
            StraightLife: level switch { > 600 => 9, > 302 => 5, > 264 => 4, > 209 => 3, > 124 => 2, > 29 => 1, _ => 0 },
            DrainingTouch: (level switch { > 600 => 10, > 275 => 3, > 219 => 2, > 34 => 1, _ => 0 }) + draining,
            BloodSpawn: (level switch { > 600 => 5, > 287 => 3, > 239 => 2, > 39 => 1, _ => 0 }) + blood,
            CoreStrike: (level switch { > 600 => 8, > 244 => 5, > 204 => 4, > 134 => 3, > 79 => 2, > 49 => 1, _ => 0 }) + fireBlast,
            KillingFlash: level switch { > 600 => 10, > 293 => 3, > 164 => 2, > 54 => 1, _ => 0 },
            MirrorOfSin: level switch { > 600 => 5, > 290 => 3, > 189 => 2, > 59 => 1, _ => 0 },
            InvisibleArrow: (level switch { > 600 => 8, > 281 => 3, > 224 => 2, > 64 => 1, _ => 0 }) + serenity,
            ShieldBreak: (level switch { > 600 => 8, > 321 => 5, > 311 => 4, > 296 => 3, > 254 => 2, > 154 => 1, _ => 0 }) + enormous,
            AbsoluteLuck: level > 600 ? 7 : level > 324 ? 5 : 0);
    }
}
