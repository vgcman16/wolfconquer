#nullable disable
using Opt = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsOpt;
using Info = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsInfo;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredPinnedWireTests
{
    public static IEnumerable<object[]> Cases()
    {
        var rows = new List<object[]>();
        void Add(string name, object value, bool error = false)
        {
            var control = Controls[name];
            if (control.Error != error) throw new InvalidDataException("Literal case kind mismatch.");
            rows.Add([name, value, control.Hex, error]);
        }
        Add("opt-default", new Opt.MsgHundredWeaponsOptProto());
        foreach (Opt.ActionID action in Enum.GetValues(typeof(Opt.ActionID)))
            Add("opt-action-" + (int)action, new Opt.MsgHundredWeaponsOptProto { Type = action });
        foreach (Weapon weapon in Enum.GetValues(typeof(Weapon)))
            Add("opt-weapon-" + (ushort)weapon, new Opt.MsgHundredWeaponsOptProto { WeaponSubtype = weapon });
        Add("opt-exchange-zero", new Opt.MsgHundredWeaponsOptProto { Exchange = new Opt.ExchangePositions() });
        Add("opt-max", new Opt.MsgHundredWeaponsOptProto
        {
            Type = Opt.ActionID.Trigger, WeaponSubtype = Weapon.Scepter, Position = Int32.MaxValue,
            Combination = UInt64.MaxValue, TriggeredStage = Byte.MaxValue, TriggeringDuration = Int32.MaxValue,
            Exchange = new Opt.ExchangePositions { Position1 = Int32.MinValue, Position2 = Int32.MaxValue }
        });
        Add("opt-negative", new Opt.MsgHundredWeaponsOptProto
        {
            Type = Opt.ActionID.Combine, WeaponSubtype = Weapon.Sword, Position = Int32.MinValue,
            Combination = 1, TriggeredStage = 1, TriggeringDuration = -1,
            Exchange = new Opt.ExchangePositions { Position1 = -1, Position2 = 0 }
        });
        Add("opt-unknown-action-five", new Opt.MsgHundredWeaponsOptProto { Type = (Opt.ActionID)5 }, true);
        Add("opt-negative-action", new Opt.MsgHundredWeaponsOptProto { Type = (Opt.ActionID)(-1) }, true);
        Add("opt-intmax-action", new Opt.MsgHundredWeaponsOptProto { Type = (Opt.ActionID)Int32.MaxValue }, true);
        Add("opt-unknown-weapon-one", new Opt.MsgHundredWeaponsOptProto { WeaponSubtype = (Weapon)1 }, true);
        Add("opt-unknown-weapon-ushortmax", new Opt.MsgHundredWeaponsOptProto { WeaponSubtype = (Weapon)UInt16.MaxValue }, true);
        Add("opt-null", null);

        Add("info-default", new Info.MsgHundredWeaponsInfoProto());
        foreach (Info.ActionID action in Enum.GetValues(typeof(Info.ActionID)))
            Add("info-action-" + (int)action, new Info.MsgHundredWeaponsInfoProto { Type = action });
        Add("info-empty", new Info.MsgHundredWeaponsInfoProto { Weapons = new Info.WeaponInfo[0] });
        Add("info-zero-row", new Info.MsgHundredWeaponsInfoProto { Weapons = new[] { new Info.WeaponInfo() } });
        Add("info-null-row", new Info.MsgHundredWeaponsInfoProto { Weapons = new Info.WeaponInfo[] { null } });
        Add("info-mixed-rows", new Info.MsgHundredWeaponsInfoProto
        {
            Type = Info.ActionID.Add, OwnerUID = 17, Weapons = new[] { null, new Info.WeaponInfo(), new Info.WeaponInfo
            {
                SubType = Weapon.Blade, AppearancePosition = 1, PositionFlags = 0x80000001,
                Level = 9, Progress = 7, Hitpoints = -1, PhysicalAttack = 2, PhysicalDefense = 3,
                MagicAttack = 4, MagicDefense = 5, Score = UInt32.MaxValue
            }, null }
        });
        Add("info-max-row", new Info.MsgHundredWeaponsInfoProto
        {
            Type = Info.ActionID.RequestInfo, OwnerUID = UInt32.MaxValue, Weapons = new[] { new Info.WeaponInfo
            {
                SubType = Weapon.Scepter, AppearancePosition = UInt32.MaxValue, PositionFlags = UInt32.MaxValue,
                Level = Byte.MaxValue, Progress = UInt32.MaxValue, Hitpoints = Int32.MinValue, PhysicalAttack = Int32.MaxValue,
                PhysicalDefense = -1, MagicAttack = 0, MagicDefense = 1, Score = UInt32.MaxValue
            } }
        });
        Add("info-wrapped-row", new Info.MsgHundredWeaponsInfoProto { Weapons = new[] { new Info.WeaponInfo
        {
            SubType = Weapon.Sword, Hitpoints = unchecked(10000 + (Int32.MinValue - Int32.MaxValue)),
            PhysicalAttack = unchecked(20000 + (Int32.MaxValue - Int32.MinValue)), Score = unchecked((uint)(-4000))
        } } });
        Add("info-unknown-action-four", new Info.MsgHundredWeaponsInfoProto { Type = (Info.ActionID)4 }, true);
        Add("info-negative-action", new Info.MsgHundredWeaponsInfoProto { Type = (Info.ActionID)(-1) }, true);
        Add("info-unknown-weapon-one", new Info.MsgHundredWeaponsInfoProto
            { Weapons = new[] { new Info.WeaponInfo { SubType = (Weapon)1 } } }, true);
        Add("info-unknown-weapon-ushortmax", new Info.MsgHundredWeaponsInfoProto
            { Weapons = new[] { new Info.WeaponInfo { SubType = (Weapon)UInt16.MaxValue } } }, true);
        Add("info-null", null);
        if (rows.Count != 77 || rows.Select(row => (string)row[0]).Distinct().Count() != 77)
            throw new InvalidDataException("The complete 77-case control set is required.");
        return rows;
    }
}
