using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

// Invented complete storage images. These controls issue no gameplay/native authority.
public sealed partial class SqlWorldStateBundleTests
{
    internal static SqlWorldStateSidecarInput[] Missing() =>
    [
        new(SqlWorldStateSidecarKind.Spells, SqlWorldStateSidecarPresence.Missing, ReadOnlyMemory<byte>.Empty),
        new(SqlWorldStateSidecarKind.Proficiencies, SqlWorldStateSidecarPresence.Missing, ReadOnlyMemory<byte>.Empty),
        new(SqlWorldStateSidecarKind.Quests, SqlWorldStateSidecarPresence.Missing, ReadOnlyMemory<byte>.Empty),
        new(SqlWorldStateSidecarKind.CombatGear, SqlWorldStateSidecarPresence.Missing, ReadOnlyMemory<byte>.Empty),
    ];

    internal static SqlWorldStateSidecarInput[] Replace(SqlWorldStateSidecarKind kind, byte[] bytes,
        SqlWorldStateSidecarPresence presence = SqlWorldStateSidecarPresence.Present)
    {
        var result = Missing(); result[(int)kind - 1] = new(kind, presence, bytes); return result;
    }

    internal static SqlWorldStateBundle Bundle(SqlWorldStateImage? core = null, SqlWorldStateSidecarInput[]? inputs = null)
    {
        Assert.True(SqlWorldStateBundle.TryCreate(core ?? SqlWorldStateTestData.Image(501, 9001), inputs ?? Missing(),
            out var bundle, out var failure), failure.ToString());
        Assert.Equal(SqlWorldStateBundleFailure.None, failure);
        return Assert.IsType<SqlWorldStateBundle>(bundle);
    }

    internal static SqlWorldStateSidecar Sidecar(SqlWorldStateBundle bundle, SqlWorldStateSidecarKind kind) =>
        Assert.Single(bundle.Sidecars.ToArray(), row => row.Kind == kind);

    // Literal independent records: deliberately unsorted IDs and nonzero opaque padding.
    internal static byte[] Spells() => Convert.FromHexString(
        "02000000" + "56040100000000000000005A" + "EA03020078563412030401A5");
    internal static byte[] Proficiencies() => Convert.FromHexString(
        "02000000" + "F40100000300000002A1B2C30403020108070605" + "900100000100000000D4E5F60000000009000000");
    // The actual quest loader permits these values. Storage must not invent gameplay restrictions.
    internal static byte[] Quests() => Convert.FromHexString(
        "02000000" + "00000000EFBEEDFEFFFFFFFF020000000500000000000000" + "00000000000000000000000000000000");

    internal static byte[] EmptyGear() => Encoding.UTF8.GetBytes(string.Join(Environment.NewLine,
    new[]
    {
        "[CombatGear]", "MasteryPoints=0", "TaoistReiki=0", "PirateExp=0", "Lottery=0", "Count=0",
        "[AirPowers]", "SenseLevel=0", "ArmedLevel=0", "OverLoadLevel=0", "Points=0",
        "SenseAipPower=0", "ArmedAipPower=0", "OverLoadAipPower=0", "[BlackJadeBag]", "Count=0", "",
    }));

    private static void Refuses(SqlWorldStateSidecarInput[] inputs, SqlWorldStateBundleFailure expected)
    {
        var before = inputs.Select(input => input.Bytes.ToArray()).ToArray();
        Assert.False(SqlWorldStateBundle.TryCreate(SqlWorldStateTestData.Image(501, 9001), inputs, out var bundle, out var failure));
        Assert.Null(bundle); Assert.Equal(expected, failure);
        for (var index = 0; index < inputs.Length; index++) Assert.Equal(before[index], inputs[index].Bytes.ToArray());
    }
}
