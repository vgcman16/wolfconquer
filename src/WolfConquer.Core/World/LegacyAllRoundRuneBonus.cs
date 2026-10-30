namespace WolfConquer.Core.World;

// Exact selected7899 RuneMagic.lua/RuneDesc.ini and official2024 Rune Details.
// Later7931/7937 item-tooltip roles disagree; this is not their activation table.
internal readonly record struct LegacyAllRoundRuneBonus(uint Attack, uint Defence, uint MagicDefence)
{
    internal static LegacyAllRoundRuneBonus ForItem(uint itemId) => itemId switch
    {
        4_020_201 => new(300, 3000, 500),
        4_020_202 => new(350, 3100, 550),
        4_020_203 => new(400, 3200, 600),
        4_020_204 => new(450, 3300, 650),
        4_020_205 => new(500, 3400, 700),
        4_020_206 => new(550, 3500, 750),
        4_020_207 => new(600, 3600, 800),
        4_020_208 => new(650, 3700, 850),
        4_020_209 => new(700, 3800, 900),
        4_020_210 => new(750, 3900, 950),
        4_020_211 => new(800, 4000, 1000),
        4_020_212 => new(850, 4100, 1050),
        4_020_213 => new(900, 4200, 1100),
        4_020_214 => new(950, 4300, 1150),
        4_020_215 => new(1000, 4400, 1200),
        4_020_216 => new(1100, 4500, 1300),
        4_020_217 => new(1200, 5000, 1400),
        4_020_218 => new(1300, 5500, 1500),
        4_020_219 => new(1400, 6000, 1600),
        4_020_220 => new(1500, 6500, 1800),
        4_020_221 => new(1600, 7000, 2000),
        4_020_222 => new(1800, 7500, 2500),
        4_020_223 => new(2000, 8000, 3000),
        4_020_224 => new(2200, 8500, 3500),
        4_020_225 => new(2500, 9000, 4000),
        4_020_226 => new(2700, 9500, 4500),
        4_020_227 => new(3000, 10000, 5000),
        _ => throw new InvalidDataException("All-round requires an exact recorded7899 item ID/level."),
    };
}
