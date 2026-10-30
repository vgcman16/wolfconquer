using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedNativeCpsPublicationTests
{
    [Theory]
    [InlineData(0L, 0L, 0L)]
    [InlineData(222L, 333L, 444L)]
    [InlineData(2000000001L, 123L, 7L)]
    [InlineData(4294967295L, 2147483647L, long.MaxValue)]
    public Task ActualAdmissionPublishesExactSavedCpTupleBeforeFirstReplyWithoutSetterSpill(long cps, long bound, long bank) =>
        WithCpsFrame(cps, bound, bank, async f =>
        {
            Exception? observation = null; int replies = 0;
            f.Native.Player.WHMoney = 77;
            f.OnSend = sentFrame =>
            {
                replies++;
                try
                {
                    Assert.Equal((checked((uint)cps), checked((int)bound), checked((ulong)bank)), NativeCps(f));
                    Assert.Equal(cps, f.Current.Character.ConquerPoints);
                    Assert.Equal(bound, f.Current.Character.BoundConquerPoints);
                    Assert.Equal(bank, f.Current.Character.BankConquerPoints);
                }
                catch (Exception error) { observation ??= error; }
            };
            var beforeUid = File.ReadAllBytes(f.Data.State);

            await f.LoginAsync(); f.OnSend = null;

            if (observation is not null) ExceptionDispatchInfo.Capture(observation).Throw();
            Assert.True(replies > 0);
            Assert.NotNull(f.Native.OrdinarySpellBinding);
            Assert.Equal((checked((uint)cps), checked((int)bound), checked((ulong)bank)), NativeCps(f));
            Assert.Equal(555L, f.Native.Player.WHMoney);
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.Data.UserPath), out _));
            Assert.Equal((cps, bound, bank), (saved.ConquerPoints, saved.BoundConquerPoints, saved.BankConquerPoints));
            Assert.Equal(beforeUid, File.ReadAllBytes(f.Data.State));
            Assert.DoesNotContain("Frame exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        });

    [Theory]
    [InlineData(4294967296L, 333L, 444L)]
    [InlineData(222L, 2147483648L, 444L)]
    public Task ValuesOutsideTheCurrentNativeRepresentationRefuseAdmissionWithoutRewriteOrAllocation(long cps, long bound, long bank) =>
        WithCpsFrame(cps, bound, bank, async f =>
        {
            // Both are legitimate signed-64 Character values; only this native
            // participant cannot represent them. Never tighten the global parser.
            var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.Data.UserPath), out _));
            Assert.Equal((cps, bound, bank), (parsed.ConquerPoints, parsed.BoundConquerPoints, parsed.BankConquerPoints));
            var files = f.Data.Files(); var before = NativeCps(f);

            await f.LoginAsync();

            Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.Null(f.Native.OrdinarySpellBinding);
            Assert.Equal(before, NativeCps(f)); Assert.Empty(f.Sent);
            f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActualBoundAndRetiredCpSettersRefuseBeforeAnyTupleFileOrOutputChange(bool retired, bool boundSetter) =>
        WithCpsFrame(222, 333, 444, async f =>
        {
            await f.LoginAsync(); var before = f.Current; var binding = f.Native.OrdinarySpellBinding!;
            if (retired)
            {
                Assert.True(f.Store.TryRemove(f.Connection, out var removed)); Assert.Same(before, removed);
                Assert.True(binding.IsRetired);
            }
            var tuple = NativeCps(f); var files = f.Data.Files(); int first = f.Sent.Count;

            var error = Assert.Throws<InvalidOperationException>(() =>
            {
                if (boundSetter) f.Native.Player.BoundConquerPoints = 334;
                else f.Native.Player.ConquerPoints = 223;
            });

            Assert.Equal("ordinaryCpsRequiresOwnedStorePlan", error.Message);
            Assert.Equal(tuple, NativeCps(f)); Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
            if (retired) Assert.False(f.Store.TryGet(f.Connection, out _)); else Assert.Same(before, f.Current);
        });

    [Theory]
    [InlineData("cp", -1L)]
    [InlineData("cp", 4294967296L)]
    [InlineData("bound", -1L)]
    [InlineData("bound", 2147483648L)]
    [InlineData("bank", -1L)]
    public Task InvalidNativeTupleProposalCannotReachTheActualStoreWriter(string slot, long value) =>
        WithCpsFrame(222, 333, 444, async f =>
        {
            await f.LoginAsync(); var before = f.Current;
            var character = slot switch
            {
                "cp" => before.Character with { ConquerPoints = value },
                "bound" => before.Character with { BoundConquerPoints = value },
                "bank" => before.Character with { BankConquerPoints = value },
                _ => throw new ArgumentOutOfRangeException(nameof(slot)),
            };
            var files = f.Data.Files(); var tuple = NativeCps(f); int first = f.Sent.Count, writes = 0;
            bool UnexpectedWriter() { writes++; throw new InvalidOperationException("Invalid CP proposal reached persistence."); }

            Assert.Throws<InvalidOperationException>(() =>
                f.Store.TryCommitSession(f.Connection, before, before with { Character = character }, UnexpectedWriter));

            Assert.Equal(0, writes); Assert.Same(before, f.Current); Assert.Equal(tuple, NativeCps(f));
            Assert.Equal(first, f.Sent.Count); f.Data.AssertFiles(files);
        });
}
