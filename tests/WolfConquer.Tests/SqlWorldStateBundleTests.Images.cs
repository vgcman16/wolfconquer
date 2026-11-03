using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed partial class SqlWorldStateBundleTests
{
    [Fact]
    public void ExplicitMissingAndValidPresentEmptyBooksRemainFourDifferentPresenceRecords()
    {
        var core = SqlWorldStateTestData.Initial().First;
        var missing = Bundle(core);
        Assert.Same(core, missing.Core); Assert.Equal(501, missing.AccountId); Assert.Equal(9001U, missing.CharacterUid);
        Assert.Equal(new uint[] { 71001, 71002, 71003 }, missing.ItemUids.ToArray());
        Assert.Equal(core.CopyCharacterBytes().Length + core.CopyItemBytes().Length, missing.TotalImageBytes);
        Assert.Equal(new[] { SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarKind.Proficiencies,
            SqlWorldStateSidecarKind.Quests, SqlWorldStateSidecarKind.CombatGear }, missing.Sidecars.ToArray().Select(row => row.Kind));
        Assert.All(missing.Sidecars.ToArray(), row =>
        { Assert.Equal(SqlWorldStateSidecarPresence.Missing, row.Presence); Assert.Equal(0, row.ByteCount); Assert.Empty(row.CopyBytes()); });

        var inputs = new[]
        {
            new SqlWorldStateSidecarInput(SqlWorldStateSidecarKind.CombatGear, SqlWorldStateSidecarPresence.Present, EmptyGear()),
            new SqlWorldStateSidecarInput(SqlWorldStateSidecarKind.Quests, SqlWorldStateSidecarPresence.Present, new byte[4]),
            new SqlWorldStateSidecarInput(SqlWorldStateSidecarKind.Proficiencies, SqlWorldStateSidecarPresence.Present, new byte[4]),
            new SqlWorldStateSidecarInput(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Present, new byte[4]),
        };
        var present = Bundle(core, inputs);
        Assert.Equal(missing.Sidecars.ToArray().Select(row => row.Kind), present.Sidecars.ToArray().Select(row => row.Kind));
        Assert.All(present.Sidecars.ToArray(), row => Assert.Equal(SqlWorldStateSidecarPresence.Present, row.Presence));
        Assert.Empty(PlayerSpellSnapshotLoader.Parse(Sidecar(present, SqlWorldStateSidecarKind.Spells).CopyBytes()).Spells);
        Assert.Empty(PlayerProficiencySnapshotLoader.Parse(Sidecar(present, SqlWorldStateSidecarKind.Proficiencies).CopyBytes()).Proficiencies);
        Assert.Empty(PlayerQuestSnapshotLoader.Parse(Sidecar(present, SqlWorldStateSidecarKind.Quests).CopyBytes()).Quests);
        Assert.Equal(missing.TotalImageBytes + 12 + EmptyGear().Length, present.TotalImageBytes);
    }

    [Fact]
    public void CapturedSlicedMemoriesPreserveAllRawPaddingAndUnsortedMembershipWithoutRetainingCallerArrays()
    {
        var raw = new[] { Spells(), Proficiencies(), Quests(), EmptyGear() };
        var expected = raw.Select(bytes => bytes.ToArray()).ToArray();
        var backing = raw.Select(bytes => new byte[] { 0xCC }.Concat(bytes).Append((byte)0xDD).ToArray()).ToArray();
        var inputs = Missing();
        for (var index = 0; index < inputs.Length; index++) inputs[index] =
            new((SqlWorldStateSidecarKind)(index + 1), SqlWorldStateSidecarPresence.Present, backing[index].AsMemory(1, raw[index].Length));
        var bundle = Bundle(inputs: inputs.Reverse().ToArray());
        Assert.Equal(new ushort[] { 1110, 1002 }, PlayerSpellSnapshotLoader.Parse(raw[0]).Spells.Select(row => row.Id));
        Assert.Equal(new uint[] { 500, 400 }, PlayerProficiencySnapshotLoader.Parse(raw[1]).Proficiencies.Select(row => row.Id));
        Assert.False(raw[0].AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(PlayerSpellSnapshotLoader.Parse(raw[0]))));
        Assert.False(raw[1].AsSpan().SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(PlayerProficiencySnapshotLoader.Parse(raw[1]))));
        var quests = PlayerQuestSnapshotLoader.Parse(raw[2]);
        Assert.Equal(2, quests.Quests.Count); Assert.All(quests.Quests, row => Assert.Equal(0U, row.Uid));
        Assert.Equal((PlayerQuestStatus)0xFEEDBEEF, quests.Quests[0].Status);
        Assert.Equal(new uint[] { 5, 0 }, quests.Quests[0].Intentions);
        foreach (var bytes in backing) Array.Clear(bytes);
        Array.Clear(inputs); Array.Clear(raw);
        for (var index = 0; index < expected.Length; index++)
        {
            var row = bundle.Sidecars[index]; var copy = row.CopyBytes(); Array.Clear(copy);
            Assert.Equal(index + 1, (int)row.Kind); Assert.Equal(expected[index].Length, row.ByteCount);
            Assert.Equal(expected[index], row.CopyBytes());
        }
        var rowCopies = bundle.Sidecars.ToArray(); Array.Clear(rowCopies);
        Assert.Equal(expected[0], bundle.Sidecars[0].CopyBytes());
    }
}
