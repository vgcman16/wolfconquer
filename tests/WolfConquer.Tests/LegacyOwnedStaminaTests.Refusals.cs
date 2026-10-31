using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStaminaTests
{
    [Theory]
    [InlineData("-1")] [InlineData("invalid")] [InlineData("65536")] [InlineData("")]
    public Task MalformedSavedStaminaRefusesBeforeOrdinaryAdmissionWritesOrPublishes(string raw) => Run(f =>
    {
        Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.Null(f.Native.OrdinarySpellBinding);
        Assert.Equal((ushort)0, f.Native.Player!.Stamina); Assert.Empty(f.Sent);
        f.Data.AssertFiles(f.BeforeLogin); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        return Task.CompletedTask;
    }, new(Stamina: raw, Admit: false));

    [Theory]
    [InlineData(27)] [InlineData(219)] [InlineData(214)] [InlineData(192)] [InlineData(240)]
    public Task RelevantUnjoinedStatusInputDeniesWithoutSpendingTheFirstDueStamp(int flag) => Run(async f =>
    {
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current.Character.Attributes, flag));
        // This is declared saved DENYING input, not a positive native status
        // producer. No native registration is fabricated to obtain a bonus.
        Assert.False(f.Native.Player!.BitVector.ArrayFlags.ContainsKey(flag)); await TickHeld(f);
        var current = f.Current; var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove("StatusFlags"); attributes.Remove("BitVector");
        f.Store.Set(f.Connection, current with { Character = current.Character with { Attributes = attributes } });
        await TickAndAssert(f, 5); // Removing only the declared negative leaves the real MinValue birth due.
    }, new(Flag: flag));

    [Fact]
    public Task ASerializedFanBitCannotMintARegistrationTimerOrStaminaBonus() => Run(async f =>
    {
        // The actual initial query owns any reconciliation; a saved bit alone
        // never becomes the source of a new positive physical194 registration.
        AssertFan(f, false); Assert.Empty(f.Native.Player!.BitVector.ArrayFlags);
        await TickAndAssert(f, 5); await TickHeld(f);
    }, new(Flag: 194));

    [Theory]
    [InlineData("trojan-learned", "0", false, 0)]
    [InlineData("trojan", "0", true, 5)]
    [InlineData("trojan-learned", "60", true, 65)]
    public Task AutomaticRampageFenceKeepsSourceInapplicableTrojanCasesWorking(string mode, string initial, bool accepted, int expected) => Run(async f =>
    {
        Assert.Equal(1000, f.Current.Character.Class);
        Assert.Equal(mode == "trojan-learned", f.Current.PlayerSpells.ContainsSpell(14190));
        if (accepted) await TickAndAssert(f, checked((ushort)expected)); else await TickHeld(f);
        Assert.False(f.Native.Player!.BitVector.ArrayFlags.ContainsKey(214));
        Assert.False(LegacyPlayerDeathState.HasStatusFlag(f.Current.Character.Attributes, 214));
    }, new(Stamina: initial, Mode: mode));

    [Fact]
    public Task ActualFakeInputRefusesAndDoesNotConsumeTheSourceFirstDueStamp() => Run(async f =>
    {
        var native = f.Native; var binding = native.OrdinarySpellBinding!; var files = f.Data.Files();
        int actor = f.Sent.Count, peer = f.PeerSent.Count;
        Assert.False(native.Fake); native.Fake = true; // DECLARED NEGATIVE raw input only.
        try
        {
            Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); await f.TickAsync();
            Assert.Equal((ushort)0, native.Player!.Stamina); Assert.Equal((ushort)0, CurrentStamina(f));
            Assert.Equal(actor, f.Sent.Count); Assert.Equal(peer, f.PeerSent.Count); f.Data.AssertFiles(files);
        }
        finally { native.Fake = false; }
        await TickAndAssert(f, 5);
    });

    [Fact]
    public Task ActualBoundStoreRejectsMalformedProposedStaminaBeforeAnyPublication() => Run(f =>
    {
        var before = f.Current; var files = f.Data.Files(); int count = f.Sent.Count;
        var values = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "-1" };
        Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, before with { Character = before.Character with { Attributes = values } }));
        Assert.Same(before, f.Current); AssertCoherent(f, 0); Assert.Equal(count, f.Sent.Count); f.Data.AssertFiles(files);
        return Task.CompletedTask;
    });

    [Fact]
    public Task RealEofRetiresReadinessAndThePhysicalPlayerNeverRegainsAnUnboundSetter() => Run(async f =>
    {
        var player = f.Native.Player!; var files = f.Data.Files();
        await f.Connection.CompleteTransportCloseAsync(); Assert.False(f.Store.TryGet(f.Connection, out _));
        int count = f.Sent.Count; await f.TickAsync(); Assert.Equal(count, f.Sent.Count); f.Data.AssertFiles(files);
        Assert.Throws<InvalidOperationException>(() => player.Stamina = 99); Assert.Equal((ushort)0, player.Stamina);
    });
}
