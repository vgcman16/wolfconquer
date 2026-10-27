namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    private uint[] PreparePrestigeComponents(PrestigeOwners owners)
    {
        var player = owners.Player;
        var points = new uint[owners.OldPoints.Length];
        checked
        {
            points[38] = owners.Weapons.PreparePrestigeScore(this);
            points[0] = PrepareJiangHuPrestige(owners.Optional.JiangHu?.Inner_Strength ?? 0);
            points[1] = PrepareChiPrestige(owners.Optional.Chi);
            points[2] = PrepareInnerPowerPrestige(owners.Optional.InnerPower);
            var level = player.Level;
            points[4] = (uint)(level * (level < 120 ? 10 : level < 130 ? 15 : level < 140 ? 20 : 25));
            points[6] = (uint)((player.Strength + player.Spirit + player.Vitality + player.Agility) * 5);
            points[7] = (uint)(player.Reborn * 1000);
            points[15] = PrepareSubClassPrestige(owners.Optional.SubClass);
            var wardrobePoints = PrepareWardrobePrestige(owners.Wardrobe.Items[1].Count);
            points[16] = wardrobePoints;
            points[17] = wardrobePoints;
            points[18] = (uint)player.NobilityRank * 1000;
            points[20] = owners.Rune.PerfectionRedRune;
            points[21] = owners.Rune.PerfectionBlueRune;
            points[22] = owners.Rune.PerfectionYellowRune;
            if (owners.Beasts.Activated)
            {
                points[37] = 300;
                for (var index = 0; index <= owners.Beasts.Level; index++)
                    points[37] += index >= 50 ? 100u : 200u;
            }
        }
        return points;
    }

    private static uint PrepareJiangHuPrestige(uint strength)
    {
        var multiplier = strength switch
        {
            0 => 0u, <= 16_200 => 30u, <= 40_500 => 33u, <= 60_750 => 36u,
            <= 72_000 => 40u, <= 79_200 => 45u, <= 80_800 => 50u, <= 81_000 => 60u, _ => 0u,
        };
        return checked(strength / 100 * multiplier);
    }

    private static uint PrepareInnerPowerPrestige(LegacyNativeInnerPowerRuntime? inner)
    {
        if (inner is null) return 0;
        var stages = inner.Stages
            ?? throw new InvalidOperationException("Prestige preparation requires available inner-power stages.");
        uint points = 0;
        foreach (var stage in stages)
        {
            if (stage is null) throw new InvalidOperationException("Prestige preparation found an invalid inner-power tail.");
            if (stage.UnLocked) points = checked(points + 100);
        }
        return points;
    }

    private static uint PrepareSubClassPrestige(LegacyNativeSubClassRuntime? subClass)
    {
        if (subClass is null) return 0;
        var classes = subClass.src
            ?? throw new InvalidOperationException("Prestige preparation requires available subclass rows.");
        uint points = 0;
        foreach (var value in classes.Values)
        {
            if (value is null) throw new InvalidOperationException("Prestige preparation found an invalid subclass tail.");
            points = checked(points + (uint)(value.Level * 80));
            points = checked(points + (uint)(value.Phrase * 20));
        }
        return points;
    }

    private static uint PrepareWardrobePrestige(int count)
    {
        uint points = 0;
        foreach (var threshold in new[] { 0, 15, 30, 45, 60 })
            if (count > threshold) points += 50;
        return points;
    }
}
