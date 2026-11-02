using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRelicFuseTests
{
    // Acceptance control for the genuine owner integration. The transitional
    // unavailable caller is intentionally insufficient; this has not been run.
    [Theory]
    [InlineData("valid")]
    [InlineData("bound")]
    public Task ActualComposeCommitsGlobalIdentityAndAllOwnersBeforeSuccess(string input) => Run(async f =>
    {
        var before = f.Current; var files = f.Data.Files(); var priorHighWater = f.HighWater;
        var spells = f.Native.MySpells!.ClientSpells;
        var binding = f.Native.OrdinarySpellBinding!;
        var savedBefore = binding.SavedSpells;
        f.BeforeSend = _ =>
        {
            f.AssertConverged();
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.Equal(priorHighWater + 1, f.HighWater);
            Assert.Empty(JsonNode.Parse(File.ReadAllBytes(f.Data.State))!["Operations"]!.AsArray());
            Assert.NotSame(savedBefore, binding.SavedSpells);
            var spellPath = Path.Combine(f.Data.Spells, FuseFixture.Uid + ".bin");
            Assert.True(binding.SavedSpells.Matches(File.ReadAllBytes(spellPath)));
        };
        try { await f.SendAsync(new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes()); }
        finally { f.BeforeSend = null; }
        Assert.Equal(2, f.Sent.Count);
        var held = f.Current.PlayerItems.RelicFuseItems;
        var result = Assert.Single(held, row => row.Position == 215);
        Assert.Equal(priorHighWater + 1, result.Uid);
        Assert.True(result.Uid > LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor);
        Assert.Equal(input == "bound" ? (byte)1 : (byte)0, result.Bound);
        Assert.Equal(4_100_001U, result.ItemId);
        Assert.Equal(new[] { FuseFixture.First, FuseFixture.Second, result.Uid }, held.Select(row => row.Uid));
        Assert.Equal(new ushort[] { 213, 214, 215 }, held.Select(row => row.Position));
        Assert.Equal(new[] { FuseFixture.Kept }, f.Current.PlayerItems.InventoryItems.Select(row => row.Uid));
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(result).ToFrameBytes(), f.Sent[0]);
        Assert.Equal(new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, result.Uid).ToFrameBytes(), f.Sent[1]);
        Assert.Same(spells, f.Native.MySpells.ClientSpells);
        Assert.Equal(before.Character.Money, f.Current.Character.Money);
        Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        Assert.Equal(before.Character.BoundConquerPoints, f.Current.Character.BoundConquerPoints);
        foreach (var (path, bytes) in files.Where(pair => !pair.Key.EndsWith("state.json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(pair.Key, Path.GetRelativePath(f.Data.Root, f.ItemsPath), StringComparison.OrdinalIgnoreCase)))
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(f.Data.Root, path)));
        f.AssertConverged();
        var committed = f.Data.Files();
        await f.SendAsync(new LegacyRelicFusePacket(0, FuseFixture.First, FuseFixture.Second, 0).ToFrameBytes());
        Assert.Equal(2, f.Sent.Count); f.Data.AssertFiles(committed);
    }, input);

    [Theory]
    [InlineData("same")]
    [InlineData("missing")]
    [InlineData("locked")]
    [InlineData("stacked")]
    public Task InvalidActualInputsRefuseWithoutUidReservationFileChangesOrSuccess(string input) => Run(async f =>
    {
        var before = f.Current; var files = f.Data.Files(); var native = f.Native;
        var image = native.OrdinarySpellBinding!.Items.Image;
        uint second = input == "same" ? FuseFixture.First : input == "missing" ? 999 : FuseFixture.Second;
        await f.SendAsync(new LegacyRelicFusePacket(0, FuseFixture.First, second, 0).ToFrameBytes());
        Assert.Empty(f.Sent); f.Data.AssertFiles(files); Assert.Same(before, f.Current);
        Assert.Same(image, native.OrdinarySpellBinding.Items.Image); Assert.True(image.IsCurrentPlain());
        f.AssertConverged();
    }, input);
}
