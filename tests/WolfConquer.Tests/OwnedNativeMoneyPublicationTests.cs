using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedNativeMoneyPublicationTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(1000L)]
    [InlineData(500000000001L)]
    [InlineData(long.MaxValue)]
    public Task ActualAdmissionLoadsSavedMoneyWithoutSetterSpillOrPokerMutation(long money) =>
        WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out var encoding);
            Assert.Contains("\nMoney=1000", text, StringComparison.Ordinal);
            text = text.Replace("\nMoney=1000", "\nMoney=" + money.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            File.WriteAllBytes(f.CharacterPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            f.Native.Player.WHMoney = 77;
            f.Native.PokerPlayer = new LegacyNativePokerPlayer("OwnedMoney", 17) { CurrentMoney = 42 };

            await f.Login();

            Assert.NotNull(f.Native.OrdinarySpellBinding);
            Assert.Equal(money, f.Current.Character.Money);
            Assert.Equal(money, f.Native.Player.Money);
            Assert.Equal(0L, f.Native.Player.WHMoney);
            Assert.Equal(42L, f.Native.PokerPlayer!.CurrentMoney);
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
                File.ReadAllBytes(f.CharacterPath), out _));
            Assert.Equal(money, saved.Money);
            Assert.DoesNotContain("Frame exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        });

    [Theory]
    [InlineData(false, -1L)]
    [InlineData(false, 1001L)]
    [InlineData(false, 500000000001L)]
    [InlineData(true, -1L)]
    [InlineData(true, 1001L)]
    [InlineData(true, 500000000001L)]
    public Task DirectSetterOnActualBoundOrRetiredPlayerRefusesBeforeAnySideEffect(bool retired, long value) =>
        WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login();
            var before = f.Current; var player = f.Native.Player;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            player.WHMoney = 77;
            f.Native.PokerPlayer = new LegacyNativePokerPlayer("OwnedMoney", 17) { CurrentMoney = 42 };
            if (retired)
            {
                Assert.True(f.Store.TryRemove(f.Connection, out var removed)); Assert.Same(before, removed);
                Assert.True(binding.IsRetired);
            }
            var files = f.Files(); int first = f.Sent.Count;

            var error = Assert.Throws<InvalidOperationException>(() => player.Money = value);

            Assert.Equal("ordinaryMoneyRequiresOwnedStorePlan", error.Message);
            Assert.Equal(before.Character.Money, player.Money);
            Assert.Equal(77L, player.WHMoney); Assert.Equal(42L, f.Native.PokerPlayer!.CurrentMoney);
            Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
            if (retired) Assert.False(f.Store.TryGet(f.Connection, out _));
            else Assert.Same(before, f.Current);
        });

    [Fact]
    public Task NegativeBoundProposalCannotReachTheActualStoreWriter() =>
        WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async f =>
        {
            await f.Login(); var before = f.Current;
            var proposal = before with { Character = before.Character with { Money = -1 } };
            var files = f.Files(); int first = f.Sent.Count, writes = 0;
            bool UnexpectedWriter()
            {
                writes++;
                throw new InvalidOperationException("Negative Money reached persistence.");
            }

            Assert.Throws<InvalidOperationException>(() => f.Store.TryCommitSession(f.Connection, before, proposal, UnexpectedWriter));

            Assert.Equal(0, writes); Assert.Same(before, f.Current);
            Assert.Equal(before.Character.Money, f.Native.Player.Money);
            Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
        });
}
