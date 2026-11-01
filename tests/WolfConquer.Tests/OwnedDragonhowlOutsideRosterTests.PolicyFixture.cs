using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlOutsideRosterTests
{
    public enum PolicyInput
    {
        UserNaN, UserInfinity, UserNegative, UserMalformed, SelectedServerNaN,
        DoubleNaN, DoubleInfinity, DoubleNegative, MaximumMalformed, Overflow,
        MissingSecondStats, NegativeSecondStats, MissingSecondThreshold, ZeroSecondThreshold,
        SafeFiniteBoundary, EligibleZero, IneligibleNaN, ValidDistinctPrecedence,
    }

    private static Task RunPolicy(ushort spell, PolicyInput input, Func<Case, Task> exercise)
    {
        var source = LegacyMagicAttackRuntimeTests.DragonhowlExperienceOverlapInputs(spell);
        bool second = input is PolicyInput.MissingSecondStats or PolicyInput.NegativeSecondStats or
            PolicyInput.MissingSecondThreshold or PolicyInput.ZeroSecondThreshold;
        var fields = new Dictionary<string, string>(source.Peer.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["MaximumMonsterKillExperienceRate"] = "10", ["RateExp"] = "1" };
        switch (input)
        {
            case PolicyInput.UserNaN: fields["UserExpRate"] = "NaN"; break;
            case PolicyInput.UserInfinity: fields["UserExpRate"] = "Infinity"; break;
            case PolicyInput.UserNegative: fields["UserExpRate"] = "-1"; break;
            case PolicyInput.UserMalformed: fields["UserExpRate"] = "not-a-rate"; break;
            case PolicyInput.SelectedServerNaN: fields.Remove("UserExpRate"); fields["ServerExperienceRate"] = "NaN"; break;
            case PolicyInput.DoubleNaN: fields["RateExp"] = "NaN"; fields["DExpTime"] = "1"; break;
            case PolicyInput.DoubleInfinity: fields["RateExp"] = "Infinity"; fields["DExpTime"] = "1"; break;
            case PolicyInput.DoubleNegative: fields["RateExp"] = "-1"; fields["DExpTime"] = "1"; break;
            case PolicyInput.MaximumMalformed: fields["MaximumMonsterKillExperienceRate"] = "bad-cap"; break;
            case PolicyInput.Overflow:
                fields["UserExpRate"] = fields["MaximumMonsterKillExperienceRate"] = "1152921504606846976"; break; // 2^60
            case PolicyInput.SafeFiniteBoundary:
                fields["UserExpRate"] = fields["MaximumMonsterKillExperienceRate"] = "576460752303423488"; break; // 2^59
            case PolicyInput.EligibleZero: fields["UserExpRate"] = "0"; break;
            case PolicyInput.IneligibleNaN: fields["UserExpRate"] = "NaN"; break;
            case PolicyInput.ValidDistinctPrecedence:
                fields["ServerExperienceRate"] = "NaN";
                fields["MaximumServerExperienceRate"] = "NaN";
                fields["DoubleExperienceRate"] = "NaN"; break;
        }
        var peer = source.Peer with { Character = source.Peer.Character with
            { X = 830, Y = 594, Attributes = fields }, OmitSpellsFile = true };
        LevelExperience Threshold(int level, long amount) => new(0, level, amount, 0, 0, level,
            $"0 {level} {amount} 0 0", new[] { "0", level.ToString(), amount.ToString(), "0", "0" });
        ClassLevelStat Stats(int level, int vitality, int spirit) => new("Taoist", level, 0, 0, vitality, spirit,
            "fixture://policy-safety/Taoist.ini", level, level.ToString(), $"0,0,{vitality},{spirit}",
            $"{level}=0,0,{vitality},{spirit}", new[] { "0", "0", vitality.ToString(), spirit.ToString() });
        var levels = new List<LevelExperience> { Threshold(10, second ? 1 : long.MaxValue), Threshold(11, second ? 10 : long.MaxValue) };
        if (input != PolicyInput.MissingSecondThreshold) levels.Add(Threshold(12, input == PolicyInput.ZeroSecondThreshold ? 0 : long.MaxValue));
        var stats = new List<ClassLevelStat> { Stats(11, 20, 20) };
        if (input != PolicyInput.MissingSecondStats) stats.Add(Stats(12, input == PolicyInput.NegativeSecondStats ? -1 : 30, 30));
        var catalog = LegacyItemWorldDataFixture.Create(Array.Empty<ItemType>(), [], classLevelStats: stats.ToArray(),
            monsters: new[] { source.Family }, magic: source.Catalog.Magic.ToArray(), levelExperience: levels.ToArray(),
            combatGear: NativeCombatGearProcFixture.CatalogRows());
        var clock = new RosterClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearOptionsAsync(source.Actor, catalog, options, async f =>
        {
            var actor = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(Assert.IsType<LegacyNativeMyCombatGear>(binding.Client.CombatGear).IsAdmissionOriginCurrent(binding, actor));
            Assert.True(f.NativeCombatGear!.IsCurrentFor(catalog));
            Assert.Equal(fields["RateExp"], f.Current(Peer).Character.Attributes["RateExp"]);
            Assert.False(File.Exists(Path.Combine(f.Data.Spells, Peer + ".bin")));
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.Create, 0).ToFrameBytes());
            await f.SendAsync(Actor, new MsgTeamPacket(MsgTeamType.InviteRequest, Peer).ToFrameBytes());
            await f.SendAsync(Peer, new MsgTeamPacket(MsgTeamType.AcceptInvitation, Actor).ToFrameBytes());
            var teams = Field<TeamSessionStateStore>(f.Handler, "_teamStates");
            Assert.Equal(2, teams.GetConnections(f.Connection(Actor)).Count);
            Assert.Contains(f.Connection(Peer), teams.GetConnections(f.Connection(Actor)));
            if (input == PolicyInput.IneligibleNaN)
            {
                // Join while genuinely visible, then leave share range through
                // the actual owned movement route. Never transplant a holder.
                await f.SendAsync(Peer, new MsgActionPacket { ObjId = Peer, TargetPositionX = 830,
                    TargetPositionY = 603, Type = MsgActionType.Jump, Facing = 0 }.ToFrameBytes());
                var moved = f.Current(Peer);
                Assert.Equal(603L, moved.Character.Y); Assert.Equal((ushort)603, moved.NativeGameClient!.Player.Y);
                Assert.Contains(f.Connection(Peer), teams.GetConnections(f.Connection(Actor)));
            }
            var monsters = Field<MonsterRuntimeStateStore>(f.Handler, "_monsterStates");
            var targets = Enumerable.Range(0, second ? 2 : 1).Select(index =>
            {
                var spawn = new MonsterSpawn(91_890 + index, 1_018, source.Family.Id, 831 + index, 584,
                    1, 1, 1, 10, 1, 0, 0, 0, 0, 1_018, source.Family.Name, "fixture://policy-safety/910.ini",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["DynamicID"] = "77" });
                var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(spawn, source.Family, monsters.NextLegacyDynamicMonsterUid()));
                Assert.Same(monster, monsters.RegisterDynamicMonster(monster, source.Family));
                Assert.True(f.Store.TryReadMonsterPhysicalRange(monster, source.Family, out _)); return monster;
            }).ToArray();
            f.ActorFrames.Clear(); f.Frames(Peer).Clear();
            await exercise(new(f, teams, monsters, targets, clock, second));
        }, peer);
    }
}
