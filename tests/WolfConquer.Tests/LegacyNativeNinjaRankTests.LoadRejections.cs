using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    public static IEnumerable<object[]> NinjaSavedNumericColumns() =>
        Enumerable.Range(0, 21).Where(index => index is not 1 and not 20).Select(index => new object[] { index });

    [Theory]
    [MemberData(nameof(NinjaSavedNumericColumns))]
    public void MalformedNumericFieldInLowScoreCappedTailCannotEscapeCompleteValidation(int column) =>
        WithOwnedRankPersistence(files =>
        {
            var fields = FullRow.Split('/');
            fields[0] = "999"; fields[3] = "0";
            fields[column] = "bad";
            var prefix = Enumerable.Range(100, 10).Select(uid => Row((uint)uid, (uint)(500 - uid), type: "2"));
            files.Seed(Text(prefix.Append(string.Join('/', fields)).Append(Row(888, type: "1")).ToArray()));
            RequireRejectedRankLoad(files);
        });

    [Theory]
    [InlineData("zero-uid")]
    [InlineData("duplicate-capped-tail")]
    [InlineData("missing-delimiter")]
    [InlineData("extra-field")]
    [InlineData("nul-uid")]
    [InlineData("nul-points")]
    [InlineData("nul-type")]
    [InlineData("nul-level")]
    [InlineData("name-control")]
    [InlineData("guild-control")]
    public void CompleteRowGrammarAndIdentityAreRequiredEvenBeyondTheRetainedTopTen(string invalid) =>
        WithOwnedRankPersistence(files =>
        {
            var fields = FullRow.Split('/');
            fields[0] = "999"; fields[3] = "0";
            switch (invalid)
            {
                case "zero-uid": fields[0] = "0"; break;
                case "duplicate-capped-tail": fields[0] = "100"; break;
                case "nul-uid": fields[0] += "\0"; break;
                case "nul-points": fields[3] += "\0"; break;
                case "nul-type": fields[2] += "\0"; break;
                case "nul-level": fields[5] += "\0"; break;
                case "name-control": fields[1] = "bad\tname"; break;
                case "guild-control": fields[20] = "bad\0guild"; break;
            }
            var tail = string.Join('/', fields);
            if (invalid == "missing-delimiter") tail = tail[..^1];
            if (invalid == "extra-field") tail += "unmodeled/";
            var prefix = Enumerable.Range(100, 10).Select(uid => Row((uint)uid, (uint)(500 - uid), type: "2"));
            files.Seed(Text(prefix.Append(tail).ToArray()));
            RequireRejectedRankLoad(files);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UndeclaredRowOrAdditionalBlankLineCannotBeIgnored(bool blank) => WithOwnedRankPersistence(files =>
    {
        files.Seed(Text(FullRow) + (blank ? Environment.NewLine : Row(99) + Environment.NewLine));
        RequireRejectedRankLoad(files);
    });

    [Fact]
    public void SixtyOneCompleteRowsRefuseWithoutChangingTheExistingRegistry() => WithOwnedRankPersistence(files =>
    {
        // Small bounded old-reader allocation, unlike the separate enormous-count cases.
        files.Seed(Text(Enumerable.Range(1, 61).Select(uid => Row((uint)uid)).ToArray()));
        RequireRejectedRankLoad(files);
    });

    [Fact]
    public void PhysicalLineOver4096CharactersRefusesBeforeAnyPublication() => WithOwnedRankPersistence(files =>
    {
        var emptyName = Row(99, name: "");
        var row = Row(99, name: new string('A', 4097 - emptyName.Length));
        Assert.Equal(4097, row.Length);
        files.Seed(Text(row));
        RequireRejectedRankLoad(files);
    });

    [Fact]
    public void FileOverOneMebibyteRefusesEvenWithAZeroHeader() => WithOwnedRankPersistence(files =>
    {
        const string header = "Count=0\n";
        files.Seed(header + new string('x', 1_048_577 - header.Length));
        RequireRejectedRankLoad(files);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AValidFileCannotInventAMissingPublicationRegistryOrGate(bool nullGate) => WithOwnedRankPersistence(files =>
    {
        files.Seed(Text(FullRow));
        var escaped = SeedPersistenceGraph();
        if (nullGate) Ranking.Ranks.SyncRoot = null!;
        else Ranking.Ranks = null!;
        var before = new PersistenceGraphImage();
        var error = Record.Exception(Ranking.Load);
        before.RequireCurrent();
        if (!nullGate) escaped.RequireEscaped();
        files.Expect(Text(FullRow));
        Assert.IsType<InvalidOperationException>(error);
    });
}
