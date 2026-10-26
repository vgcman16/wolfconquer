using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private LegacyNativeCombatGearTable.MailboxLoadProof? mailboxRecoveryTables;
    private bool MailboxRecoveryTablesCurrent() => mailboxRecoveryTables is null || mailboxRecoveryTables.IsCurrent();

    // Full native CombatGear.Load storage sequence, on a detached real owner.
    // Explicit canonical input replaces the global INI prefix; required metadata
    // misses/duplicate bag rune types reject instead of silently dropping rows.
    private static LegacyNativeMyCombatGear RestoreMailboxGear(LegacyNativeGameClientRuntime client,
        CombatGearSnapshot saved, uint job, LegacyNativeCombatGearTable.MailboxLoadProof tables)
    {
        var gear = new LegacyNativeMyCombatGear(client)
        {
            MasteryPoints = saved.MasteryPoints, TaoistReiki = saved.TaoistReiki,
            PirateExp = saved.PirateExp, Lottery = saved.Lottery,
        };
        var air = saved.AirPowers;
        gear.AirPowers.SenseLevel = air.SenseLevel;
        gear.AirPowers.ArmedLevel = air.ArmedLevel;
        gear.AirPowers.OverLoadLevel = air.OverLoadLevel;
        gear.AirPowers.Points = air.Points;
        gear.AirPowers.SenseAipPower = air.SenseAipPower;
        gear.AirPowers.ArmedAipPower = air.ArmedAipPower;
        gear.AirPowers.OverLoadAipPower = air.OverLoadAipPower;
        foreach (var stored in saved.Items)
        {
            if (stored.ItemID == 0 || !tables.TryGetGear(stored.ItemID,
                    stored.Level > 1 ? stored.Level - 1 : stored.Level, out _))
                throw MailboxRewardUnavailable();
            var item = new Weapon
            {
                ItemID = (TypeID)stored.ItemID, Level = stored.Level, Exp = stored.Exp, Equip = stored.Equip,
                Transform = stored.Transform, MasteryScore = stored.MasteryScore,
            };
            for (var row = 0; row < 2; row++)
                for (var column = 0; column < 7; column++) item.TypeAnima[row][column] = stored.Animas[row][column];
            for (var index = 0; index < 6; index++)
            {
                var type = stored.BlackJades[index];
                if (type != 0 && !tables.TryGetJade(type, out _)) throw MailboxRewardUnavailable();
                item.BlackJades[index].Type = type;
            }
            if (!gear.Items.TryAdd(item.ItemID, item)) throw MailboxRewardUnavailable();
        }
        var runeTypes = new HashSet<uint>();
        foreach (var id in saved.BlackJadeBag.BlackJades)
        {
            if (id == 0 || !tables.TryGetJade(id, out var row) || !runeTypes.Add(row!.TypeRune) ||
                gear.AddBlackJade(id, 0) is null) throw MailboxRewardUnavailable();
        }
        // Explicit cold recovery identity adoption uses the actual plain source
        // SetClass setter; it does not claim MyCombatGear.Loading's other effects.
        gear.SetClass(job);
        gear.mailboxRecoveryTables = tables;
        return gear;
    }
}
