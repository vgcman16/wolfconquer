using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Fact]
    public Task RealJointHitPreservesCaseDistinctOpaqueAttributesOnBothOwners() =>
        RunProficiencyAsync(true, async f =>
        {
            foreach (var uid in new[] { Actor, Target })
            {
                var before = f.Current(uid);
                var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.Ordinal)
                    { ["Other"] = "upper", ["other"] = "lower" };
                f.Owner.Store.Set(f.Owner.Connection(uid), before with
                    { Character = before.Character with { Attributes = attributes } });
                Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(f.Binding(uid)));
            }

            await AssertProficiencyHitAsync(f, existing: true);
            foreach (var uid in new[] { Actor, Target })
            {
                Assert.Equal("upper", f.Current(uid).Character.Attributes["Other"]);
                Assert.Equal("lower", f.Current(uid).Character.Attributes["other"]);
                Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(f.Binding(uid)));
            }
        });

    private sealed record ProficiencyRefusal(WorldSession ActorSession, WorldSession TargetSession,
        Dictionary<string, byte[]> Files, DateTime Stamp, object Map, object[] Rows);

    private static ProficiencyRefusal CaptureProficiencyRefusal(AttackFixture f)
    {
        Assert.Empty(f.Order); Assert.False(f.Binding().Client.OnAutoAttack);
        var map = f.Binding().Client.MyProfs!.ClientProf;
        return new(f.Current(), f.Current(Target), f.Files(), f.Binding().Player.AttackStamp,
            map, map.Values.Cast<object>().ToArray());
    }

    private static void AssertProficiencyRefusal(AttackFixture f, ProficiencyRefusal before)
    {
        f.AssertFiles(before.Files); Assert.Empty(f.Order);
        Assert.Same(before.ActorSession, f.Current()); Assert.Same(before.TargetSession, f.Current(Target));
        f.AssertCoherent(2000); Assert.Equal(before.Stamp, f.Binding().Player.AttackStamp);
        Assert.Equal(250U, (uint)f.Binding().Player.Action); Assert.False(f.Binding().Client.OnAutoAttack);
        var map = f.Binding().Client.MyProfs!.ClientProf; Assert.Same(before.Map, map);
        Assert.Equal(before.Rows.Length, map.Count);
        foreach (var row in before.Rows) Assert.Contains(map.Values, actual => ReferenceEquals(actual, row));
        AssertProficiencyMap(f, before.ActorSession.PlayerProficiencies);
    }

    private static async Task AssertProficiencyHitAsync(AttackFixture f, bool existing)
    {
        var before = f.Files(); var beforeProfs = File.ReadAllBytes(ProficiencyPath(f));
        var afterProfs = ProficiencyAfterImage(beforeProfs, existing);
        var hpCommitted = HpImage(f, before, 2000, 1700);
        var profCommitted = hpCommitted.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        profCommitted[Path.GetRelativePath(f.Owner.Data.Root, ProficiencyPath(f))] = afterProfs;
        var afterBook = PlayerProficiencySnapshotLoader.Parse(afterProfs);
        var unrelated = f.Binding().Client.MyProfs!.ClientProf[420];
        var stamp = f.Clock.GetLocalNow().DateTime;
        bool sawProficiency = false;
        f.BeforeSend = (recipient, bytes) =>
        {
            var frame = OwnedEquipmentBehaviorFixture.ReadFrame(bytes);
            if (LegacyProficiencyPacket.TryParse(frame, out var proficiency))
            {
                Assert.False(sawProficiency); sawProficiency = true;
                Assert.Equal(Actor, recipient); Assert.Equal(410U, proficiency.Id);
                Assert.Equal(ProficiencyGolden(existing), bytes);
            }
            // Joint journal24 publishes both durable participants and their
            // native holders before the first interaction frame.
            f.AssertFiles(profCommitted);
            f.AssertCoherent(1700); AssertCommittedActor(f, stamp);
            AssertProficiencyMap(f, afterBook);
            Assert.Same(unrelated, f.Binding().Client.MyProfs!.ClientProf[420]);
            // Real source repeat is armed only after accepted progression output.
            Assert.False(f.Binding().Client.OnAutoAttack);
        };
        try { await f.AttackAsync(); } finally { f.BeforeSend = null; }
        Assert.True(sawProficiency); Assert.Equal(5, f.Order.Count);
        f.AssertFiles(profCommitted); f.AssertCoherent(1700); AssertCommittedActor(f, stamp);
        AssertProficiencyMap(f, afterBook); Assert.True(f.Binding().Client.OnAutoAttack);
        Assert.Equal(new[] { Actor, Target, Target, Target, Actor }, f.Order.Select(row => row.Recipient));
        Assert.Equal(InteractionGolden(), f.Order[0].Bytes); Assert.Equal(InteractionGolden(), f.Order[1].Bytes);
        Assert.Equal(MaximumGolden(Target), f.Order[2].Bytes); Assert.Equal(HpGolden(Target, 1700), f.Order[3].Bytes);
        Assert.Equal(ProficiencyGolden(existing), f.Order[4].Bytes);
        int count = f.Order.Count; await f.TickAsync();
        Assert.Equal(count, f.Order.Count); f.AssertFiles(profCommitted);
        // The first source20-byte row's opaque tail remains literal, and any
        // unrelated full420 row remains unchanged even when410 was appended.
        Assert.Equal(existing ? new byte[] { 0xA5, 0xC7, 0xD9 } : new byte[] { 0x31, 0xA7, 0xF2 },
            File.ReadAllBytes(ProficiencyPath(f)).AsSpan(13, 3).ToArray());
    }

    private static void AssertProficiencyMap(AttackFixture f, PlayerProficiencySnapshot expected)
    {
        Assert.Equal(expected.Proficiencies, f.Current().PlayerProficiencies.Proficiencies);
        var map = f.Binding().Client.MyProfs!.ClientProf;
        Assert.Equal(expected.Proficiencies.Select(row => row.Id).Order(), map.Keys.Order());
        foreach (var row in expected.Proficiencies)
        {
            var native = map[row.Id]; Assert.Equal(row.Id, native.ID); Assert.Equal(row.Level, native.Level);
            Assert.Equal(row.PreviousLevel, native.PreviouseLevel); Assert.Equal(row.Experience, native.Experience);
            Assert.Equal(row.NeededExperience, native.NeededExperience);
        }
    }

    private static byte[] ProficiencyGolden(bool existing)
    {
        // Literal current2041 header +fourUInt32 fields; no production writer.
        var bytes = new byte[20]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2041);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 410);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), existing ? 5U : 0U);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), existing ? 50_100U : 0U);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), existing ? 1_600_000U : 0U);
        return bytes;
    }
}
