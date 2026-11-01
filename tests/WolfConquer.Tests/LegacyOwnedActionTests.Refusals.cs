using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData(null, 0U)] [InlineData("250", 250U)] [InlineData("65786", 65786U)]
    [InlineData("MagicDefender", 273344U)] [InlineData("sIt", 250U)]
    public Task ActualAdmissionOwnsAbsentZeroAndTheExplicitFullWidthSavedCompatibilityPolicy(string? raw, uint value) => Run(f =>
    {
        AssertCoherent(f, value);
        var saved = f.Data.Loader.LoadByAccountId(Uid)!;
        if (raw is null) Assert.False(saved.Attributes.ContainsKey("Action"));
        else Assert.Equal(raw, saved.Attributes["Action"]);
        Assert.Equal(value.ToString(System.Globalization.CultureInfo.InvariantCulture), f.Current.Character.Attributes["Action"]);
        Assert.False(f.Native.FullLoading); Assert.Equal((ushort)0, f.Native.Player!.Stamina);
        return Task.CompletedTask;
    }, new(Action: raw));

    [Theory]
    [InlineData("-1")] [InlineData("4294967296")] [InlineData("not-a-number")]
    [InlineData("")] [InlineData("Sit, Wave")]
    public Task MalformedSavedActionRefusesBeforeAdmissionAndLaterStanceOrJumpCannotCreateAnOwner(string raw) => Run(async f =>
    {
        AssertNoActionAdmission(f); f.Data.AssertFiles(f.BeforeLogin);
        Assert.Equal(raw, f.Data.Loader.LoadByAccountId(Uid)!.Attributes["Action"]);
        await f.StanceAsync(250); await f.JumpAsync();
        AssertNoActionAdmission(f); f.Data.AssertFiles(f.BeforeLogin);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }, new(Action: raw, Admit: false));

    [Fact]
    public Task BoundMalformedActionCannotPartiallyPublishAnotherPhysicalResource() => Run(async f =>
    {
        await f.StanceAsync(65786); ClearOutput(f);
        var current = f.Current; var files = f.Data.Files();
        var fields = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Action"] = "4294967296" };
        Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, current with
        { Character = current.Character with { MinMana = 1, Attributes = fields } }));
        Assert.Same(current, f.Current); AssertFilesAndOwner(f, files, 65786);
        Assert.Empty(f.Sent); Assert.Empty(f.PeerSent); Assert.Null(f.Current.MagicCooldowns);
    });

    private static void AssertNoActionAdmission(ActionFixture f)
    {
        Assert.Empty(f.Store.Snapshot()); Assert.Empty(f.Sent); Assert.Empty(f.PeerSent);
        foreach (var uid in new[] { Uid, Peer })
        {
            var native = f.ConnectionFor(uid).NativeGameClient!;
            Assert.Null(native.OrdinarySpellBinding); Assert.Null(native.MySpells); Assert.Null(native.MyProfs);
            Assert.Null(native.Inventory); Assert.Null(native.Equipment); Assert.Null(native.Rune);
            Assert.Equal(0U, native.Player!.UID); Assert.Equal(0U, (uint)native.Player.Action);
            Assert.Equal(0, native.Player.HitPoints); Assert.Equal(0U, native.Player.Mana); Assert.Equal((ushort)0, native.Player.Stamina);
            Assert.Empty(native.Player.BitVector.ArrayFlags); Assert.False(native.FullLoading);
        }
    }
}
