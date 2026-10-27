using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    public static IEnumerable<object[]> PrestigeRebornMalformedSelectedOwners()
    {
        foreach (var fault in new[]
        {
            "null-registry", "missing-selected", "null-selected", "wrong-type", "zero-cap", "negative-cap",
            "null-items", "null-tail", "zero-key-tail", "mismatched-uid-tail", "rank-alias", "null-gate",
        })
        foreach (var targetPresent in new[] { true, false })
            yield return [fault, targetPresent];
    }

    [Theory]
    [MemberData(nameof(PrestigeRebornMalformedSelectedOwners))]
    public void RebornSafetyMalformedSelectedStateRefusesEvenWhenTheTargetUidIsAbsent(string fault, bool targetPresent) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(targetPresent ? (byte)1 : (byte)2);
            var selected = RebornSelectedOwner(user, targetPresent);
            var graph = Ranking.Ranks;
            var category = selected._Type;
            _ = graph.GetValues();
            switch (fault)
            {
                case "null-registry": Ranking.Ranks = null!; break;
                case "missing-selected": graph.Remove(category); break;
                case "null-selected": graph[category] = null!; break;
                case "wrong-type": selected._Type = Ranking.Type.World; break;
                case "zero-cap": selected.MaxItems = 0; break;
                case "negative-cap": selected.MaxItems = -1; break;
                case "null-items": selected.Items = null!; break;
                case "null-tail": selected.MaxItems = 1; selected.Items.Add(77, null!); break;
                case "zero-key-tail": selected.MaxItems = 1; selected.Items.Add(0, Entry(0, 1)); break;
                case "mismatched-uid-tail": selected.MaxItems = 1; selected.Items.Add(77, Entry(88, 1)); break;
                case "rank-alias": graph.Add(Ranking.Type.World, selected); break;
                case "null-gate": Ranking.SynRoot = null!; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var before = new PrestigeRebornGraphImage(graph);
            var selectedBefore = new PrestigeMutationRankImage(selected);
            var clientBefore = new PrestigeRebornUserImage(user);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            selectedBefore.RequireUnchanged();
            clientBefore.RequireUnchanged();
            if (fault == "null-gate") Assert.IsType<ArgumentNullException>(error);
            else Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData((byte)0, "missing-player")]
    [InlineData((byte)1, "missing-player")]
    [InlineData((byte)2, "missing-player")]
    [InlineData((byte)0, "foreign-same-uid")]
    [InlineData((byte)1, "foreign-same-uid")]
    [InlineData((byte)2, "foreign-same-uid")]
    [InlineData((byte)1, "foreign-other-uid")]
    [InlineData((byte)2, "foreign-other-uid")]
    [InlineData((byte)1, "foreign-same-family")]
    [InlineData((byte)2, "foreign-same-family")]
    public void RebornSafetyRequiresTheActualClientsConstructorBoundPlayerBeforeAnyCleanup(byte reborn, string fault) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(reborn);
            var originalPlayer = user.Player;
            // Seed the same historical owner even for a non-reborn input; no
            // malformed foreign object becomes authority merely by matching UID.
            var category = reborn == 2 ? Ranking.Type.Warrior : Ranking.Type.Trojan;
            var selected = new Ranking.Rank(category) { MaxItems = 30 };
            selected.AddItem(user.PrestrigeEntry!);
            Ranking.Ranks.Add(category, selected);
            var foreign = RebornUser(reborn, uid: fault == "foreign-other-uid" ? 99u : 41u);
            if (fault == "foreign-same-family") SetRebornCurrentClass(foreign, reborn == 1 ? 1001u : 2001u);
            if (fault == "foreign-other-uid") selected.AddItem(foreign.PrestrigeEntry!);
            user.Player = fault == "missing-player" ? null! : foreign.Player;
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);
            var foreignBefore = new PrestigeRebornUserImage(foreign);
            var originalFields = RebornPlayerFields(originalPlayer);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
            foreignBefore.RequireUnchanged();
            Assert.Equal(originalFields, RebornPlayerFields(originalPlayer));
            Assert.IsType<InvalidOperationException>(error);
        });

    [Theory]
    [InlineData((byte)1, false)]
    [InlineData((byte)1, true)]
    [InlineData((byte)2, false)]
    [InlineData((byte)2, true)]
    public void RebornSafetyActiveZeroUidRefusesBeforeAnySelectedRankMutationOrSameFamilySkip(byte reborn, bool sameFamily) =>
        RunPrestigeMutationSafety(() =>
        {
            var user = RebornUser(reborn);
            RebornSelectedOwner(user);
            if (sameFamily) SetRebornCurrentClass(user, reborn == 1 ? 1001u : 2001u);
            user.Player.UID = 0;
            var before = new PrestigeRebornGraphImage();
            var clientBefore = new PrestigeRebornUserImage(user);

            var error = Record.Exception(() => Ranking.CheckReborn(user));

            before.RequireUnchanged();
            clientBefore.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Fact]
    public void RebornSafetyNullUserRefusesBeforeAnyRankMutation() =>
        RunPrestigeMutationSafety(() =>
        {
            RebornSelectedOwner(RebornUser(1));
            var before = new PrestigeRebornGraphImage();

            var error = Record.Exception(() => Ranking.CheckReborn(null!));

            before.RequireUnchanged();
            Assert.IsType<ArgumentNullException>(error);
        });
}
