using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGuildWarGateRuntimeResetTests
{
    [Fact]
    public void ResetGateOwnersUsesPositiveGuildWarInfoIdForBothGuildWarGates()
    {
        var states = new SobNpcRuntimeStateStore();
        var leftGate = SobNpc(LegacySobNpcEventRules.GuildWarLeftGateUid, "LeftGate");
        var rightGate = SobNpc(LegacySobNpcEventRules.GuildWarRightGateUid, "RightGate");
        var nonGate = SobNpc(25_005, "AttackPole");
        var guildWarInfo = GuildWarData(infoId: 1_363);

        var resetCount = LegacyGuildWarGateRuntimeReset.ResetGateOwners(
            guildWarInfo,
            [leftGate, rightGate, nonGate],
            states);

        Assert.Equal(2, resetCount);
        Assert.Equal(1_363U, states.GetOwnerGuildId(leftGate));
        Assert.Equal(1_363U, states.GetOwnerGuildId(rightGate));
        Assert.Equal(0U, states.GetOwnerGuildId(nonGate));
    }

    [Fact]
    public void ResetGateOwnersSkipsMissingOrZeroGuildWarInfoId()
    {
        var states = new SobNpcRuntimeStateStore();
        var leftGate = SobNpc(LegacySobNpcEventRules.GuildWarLeftGateUid, "LeftGate");

        Assert.Equal(
            0,
            LegacyGuildWarGateRuntimeReset.ResetGateOwners(
                GuildWarData(infoId: 0),
                [leftGate],
                states));
        Assert.Equal(0U, states.GetOwnerGuildId(leftGate));

        Assert.Equal(
            0,
            LegacyGuildWarGateRuntimeReset.ResetGateOwners(
                GuildWarData(infoId: null),
                [leftGate],
                states));
        Assert.Equal(0U, states.GetOwnerGuildId(leftGate));
    }

    [Fact]
    public void ResetGateOwnersClearsDemolitionStateSoTypeTenCanBeSentAfterReset()
    {
        var states = new SobNpcRuntimeStateStore();
        var leftGate = SobNpc(LegacySobNpcEventRules.GuildWarLeftGateUid, "LeftGate");
        var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");
        states.ApplyDamage(
            leftGate,
            requestedDamage: 20_000_000,
            now,
            LegacySobNpcEventRules.GetDeathBehavior(leftGate));
        Assert.True(states.TryMarkGuildWarGateDestroyed(leftGate));

        var resetCount = LegacyGuildWarGateRuntimeReset.ResetGateOwners(
            GuildWarData(infoId: 1_363),
            [leftGate],
            states);
        var visible = states.GetVisible(leftGate);

        Assert.Equal(1, resetCount);
        Assert.Equal(leftGate.Lookface, visible.Lookface);
        Assert.Equal(leftGate.MaxLife, visible.Life);
        Assert.Equal(1_363U, states.GetOwnerGuildId(leftGate));
        Assert.True(states.TryMarkGuildWarGateDestroyed(leftGate));
    }

    [Fact]
    public void ResetGateOwnersUsesWinnerGuildConstructForHitpointsAndMeshes()
    {
        var root = CreateTempRoot();
        try
        {
            var usersRoot = Path.Combine(root, "DBServer", "Users");
            var guildsRoot = Path.Combine(root, "DBServer", "Guilds");
            Directory.CreateDirectory(usersRoot);
            Directory.CreateDirectory(guildsRoot);
            File.WriteAllLines(
                Path.Combine(guildsRoot, "1363.txt"),
                [
                    "Count=10",
                    "1363/fredoome/9/elkomy/46998100/0/20221217/None/0/0/1296374/0/0/91126/0/0/0/15/1296374/",
                    "0/",
                    "0/",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0",
                    "0/",
                    "3/2/20/0/1/0/0/0/1/20/0/0/0/0/0/3/1/0/4/0/0/0/",
                ]);
            var states = new SobNpcRuntimeStateStore();
            var leftGate = SobNpc(LegacySobNpcEventRules.GuildWarLeftGateUid, "LeftGate");
            var rightGate = SobNpc(LegacySobNpcEventRules.GuildWarRightGateUid, "RightGate");
            var formation = new SyndicateFormationType(
                Id: 20,
                Type: 100,
                Level: 20,
                Values: [0, 160_000_000L, 800, 3_000, 0],
                SourceFile: "fixture://syn_formtype.txt",
                LineNumber: 20,
                RawLine: "",
                Fields: []);

            var resetCount = LegacyGuildWarGateRuntimeReset.ResetGateOwners(
                GuildWarData(infoId: 1_363),
                [leftGate, rightGate],
                states,
                usersRoot,
                [formation]);
            var leftVisible = states.GetVisible(leftGate);
            var rightVisible = states.GetVisible(rightGate);

            Assert.Equal(2, resetCount);
            Assert.Equal(160_000_000, leftVisible.Life);
            Assert.Equal(160_000_000, leftVisible.MaxLife);
            Assert.Equal(LegacySobNpcEventRules.GuildWarLeftGateClose4Mesh, leftVisible.Lookface);
            Assert.Equal(160_000_000, rightVisible.Life);
            Assert.Equal(160_000_000, rightVisible.MaxLife);
            Assert.Equal(LegacySobNpcEventRules.GuildWarRightGateClose4Mesh, rightVisible.Lookface);

            var now = DateTimeOffset.Parse("2026-06-12T00:00:00Z");
            var death = states.ApplyDamage(
                leftGate,
                requestedDamage: 200_000_000,
                now,
                LegacySobNpcEventRules.GetDeathBehavior(leftGate));
            var leftAfterDeath = states.GetVisible(leftGate);

            Assert.True(death.Killed);
            Assert.Equal(160_000_000U, death.DamageApplied);
            Assert.Equal(160_000_000U, death.RemainingHitpoints);
            Assert.Equal(160_000_000, leftAfterDeath.Life);
            Assert.Equal(160_000_000, leftAfterDeath.MaxLife);
            Assert.Equal(LegacySobNpcEventRules.GuildWarLeftGateDemolition4Mesh, leftAfterDeath.Lookface);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void ResetGateOwnersFallsBackToSeedGateWhenConstructDataIsMissing()
    {
        var states = new SobNpcRuntimeStateStore();
        var leftGate = SobNpc(LegacySobNpcEventRules.GuildWarLeftGateUid, "LeftGate");

        var resetCount = LegacyGuildWarGateRuntimeReset.ResetGateOwners(
            GuildWarData(infoId: 1_363),
            [leftGate],
            states,
            usersRoot: null,
            syndicateFormationTypes: []);
        var visible = states.GetVisible(leftGate);

        Assert.Equal(1, resetCount);
        Assert.Equal(10_000_000, visible.Life);
        Assert.Equal(10_000_000, visible.MaxLife);
        Assert.Equal(leftGate.Lookface, visible.Lookface);
    }

    private static GuildWarData GuildWarData(int? infoId)
    {
        return new GuildWarData(
            "fixture://GuildWarInfo.ini",
            [],
            [],
            [],
            new GuildWarInfoData(
                infoId,
                Name: null,
                LeaderReward: null,
                DeputiLeaderReward: null,
                LeaderTop: new Dictionary<int, int>(),
                DeputiTop: new Dictionary<int, int>()),
            pole: null,
            conductors: []);
    }

    private static SobNpc SobNpc(int id, string name)
    {
        return new SobNpc(
            Id: id,
            Name: name,
            Kind: LegacySobNpcEventRules.GateKind,
            Lookface: id == LegacySobNpcEventRules.GuildWarLeftGateUid ? 1_008 : 1_007,
            MapId: LegacySobNpcEventRules.GuildWarMapId,
            X: 85,
            Y: 70,
            Life: 10_000_000,
            MaxLife: 10_000_000,
            Unknown9: 1,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: id,
            RawLine: $"{id},{name},26,1008,1038,85,70,10000000,10000000,1,0",
            Fields: []);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "WolfConquer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
