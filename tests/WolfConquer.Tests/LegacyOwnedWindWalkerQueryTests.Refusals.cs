using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    [Fact]
    public void DataPresentNullAliasIsMalformedRatherThanAnAbsentOrEmptyOwnedVector()
    {
        // DATA only: saved text cannot represent a present runtime null value.
        // This reader control does not mint an owner, registration or query.
        foreach (var alias in new[] { "StatusFlags", "BitVector" })
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [alias] = null! };
            var character = OwnedEquipmentBehaviorFixture.Character(Uid) with { Attributes = attributes };
            Assert.Throws<InvalidDataException>(() => LegacyWindWalkerStatusProjection.Read(character));
            Assert.True(attributes.ContainsKey(alias)); Assert.Null(attributes[alias]);
        }
    }

    [Theory]
    [InlineData("bad", null)] [InlineData("0/0/0/0/0/0/0", "0/0/0/0/0/0/4")]
    public Task DeclaredMalformedOrConflictingManagedAliasesNeverMintAFlagOrRepairTheSavedInput(string flags, string? alias) => Run(async f =>
    {
        Assert.Null(f.Row); Assert.Equal(0U, f.Vector.bits[6] & 4);
        var files = f.Owner.Data.Files(); int first = f.Sent.Count;
        await f.QueryAsync(); Assert.Null(f.Row); Assert.Equal(0U, f.Vector.bits[6] & 4);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _)); f.Owner.Data.AssertFiles(files);
    }, mode: "pair", flags: flags, alias: alias);

    [Theory]
    [InlineData("bitmap")] [InlineData("registration-key")] [InlineData("registration-membership")]
    public Task ActualPostPerfectionNativeGraphCorruptionRefusesWithoutRenewalRepairOrSuccess(string fault) => Run(async f =>
    {
        AssertJoined(f, true); var row = f.Row!; var vector = f.Vector; var timer = row.Timer;
        var registrations = vector.ArrayFlags; var words = vector.bits; var beforeWords = (uint[])words.Clone();
        var files = f.Owner.Data.Files(); int first = f.Sent.Count; bool injected = false;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Uid || injected || !LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
            injected = true;
            // DECLARED NEGATIVE escaped raw mutations, never a positive owner.
            if (fault == "bitmap") words[0] ^= 1;
            else if (fault == "registration-key") row.Key = Fan + 1;
            else Assert.True(registrations.TryRemove(Fan, out var removed) && ReferenceEquals(removed, row));
        };
        try
        {
            await f.QueryAsync(); Assert.True(injected); Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
            Assert.Same(registrations, vector.ArrayFlags); Assert.Same(words, vector.bits); Assert.Equal(timer, row.Timer);
            f.Owner.Data.AssertFiles(files);
        }
        finally
        {
            f.BeforeSend = null; row.Key = Fan; beforeWords.CopyTo(words, 0);
            if (fault == "registration-membership") Assert.True(registrations.TryAdd(Fan, row));
        }
        AssertJoined(f, true); Assert.Same(row, f.Row);
    }, mode: "pair");

    [Fact]
    public Task ActualEofAtPerfectionPreventsThePostludeAndKeepsTheCompletedEquipmentPhase() => Run(async f =>
    {
        AssertJoined(f, false); var native = f.Native; Task? close = null; Dictionary<string, byte[]>? committed = null;
        int first = f.Sent.Count;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Uid || !LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
            Assert.Null(close); committed = f.Owner.Data.Files();
            Assert.Equal((ushort)5, Assert.Single(f.Current.PlayerItems.EquipmentItems, row => row.Uid == Left).Position);
            // EOF marks the real Trades lifetime synchronously. Await holder
            // retirement only after the active Frame releases its shared lease.
            close = f.Owner.Connection(Uid).CompleteTransportCloseAsync();
        };
        try { await f.EquipAsync(); } finally { f.BeforeSend = null; }
        Assert.NotNull(close); await close!; Assert.NotNull(committed); f.Owner.Data.AssertFiles(committed!);
        Assert.False(f.Store.TryGet(f.Owner.Connection(Uid), out _)); Assert.Equal(0U, native.Player!.BitVector.bits[6] & 4);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(f.Sent[^1]), out _));
        await f.Owner.Runtime.DisposeAsync();
        for (int restart = 0; restart < 2; restart++)
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(f.Owner.Data.Loader, f.Owner.Data.Options(), out var opened, out var reason), reason);
            await using var owner = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened); f.Owner.Data.AssertFiles(committed!);
        }
    });

    [Fact]
    public Task ASecondRealQueryAtPerfectionRetiresTheFirstQueryEpochWithoutAnotherAddOrRenewal() => Run(async f =>
    {
        AssertJoined(f, false); bool entered = false; Task? nested = null; int first = f.Sent.Count;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Uid || entered || !LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
            entered = true; nested = f.QueryAsync();
            // Both are actual ordinary Shared query routes, not direct Reset or
            // an injected epoch. Never block awaiting a queued exclusive action.
            Assert.True(nested.IsCompleted, "The nested ordinary query must finish before the outer perfection sender returns.");
            nested.GetAwaiter().GetResult();
        };
        try { await f.EquipAsync(); } finally { f.BeforeSend = null; if (nested is not null) await nested; }
        Assert.True(entered); AssertJoined(f, true); var row = f.Row!; var timer = row.Timer;
        var output = f.Sent.Skip(first).ToArray(); Assert.Single(output, bytes => IsVector(bytes, out _));
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(output[^1]), out _));
        first = f.Sent.Count; await f.QueryAsync(); Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
    });

    [Fact]
    public Task ActualCurrentClassChangeAtPerfectionInvalidatesTheCapturedEligibilityBeforeFlagMutation() => Run(async f =>
    {
        AssertJoined(f, false); bool changed = false; Dictionary<string, byte[]>? afterActualWrite = null; int first = f.Sent.Count;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Uid || changed || !LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
            changed = true; var current = f.Current;
            // Real bound Store scalar publication/current-input change. This
            // is not a gameplay class-change authorization or fake query owner.
            f.Store.Set(f.Owner.Connection(Uid), current with { Character = current.Character with { Class = 10050 } });
            afterActualWrite = f.Owner.Data.Files();
        };
        try { await f.EquipAsync(); } finally { f.BeforeSend = null; }
        Assert.True(changed); Assert.Equal(10050, f.Current.Character.Class); AssertJoined(f, false);
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _)); Assert.NotNull(afterActualWrite);
        AssertUnchangedSaved(f, afterActualWrite!);
    });

    [Fact]
    public Task ActualInputChangeAtAppendRetiresTheOldEpochBeforeTheNextQueryBeginRefuses() => Run(async f =>
    {
        AssertJoined(f, true); var row = f.Row!; var timer = row.Timer;
        Assert.True(LegacyNativeMagicQueryClient.TryRead(f.Store, f.Owner.Connection(Uid), Uid, out _));
        int first = f.Sent.Count; bool changed = false; Dictionary<string, byte[]>? afterActualWrite = null;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Uid || changed || !LegacyShowEquipmentPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var append) ||
                append.Action != LegacyShowEquipmentPacket.ShowAction) return;
            changed = true; var current = f.Current;
            // The actual execution already captured its equipment/source. A
            // real bound Store scalar writer now changes that current input;
            // no private epoch, decoded DTO or custom collection is injected.
            f.Store.Set(f.Owner.Connection(Uid), current with { Character = current.Character with { Class = 10050 } });
            afterActualWrite = f.Owner.Data.Files();
        };
        try { await f.QueryAsync(); } finally { f.BeforeSend = null; }
        Assert.True(changed); Assert.Equal(10050, f.Current.Character.Class);
        Assert.False(LegacyNativeMagicQueryClient.TryRead(f.Store, f.Owner.Connection(Uid), Uid, out _));
        Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer); AssertJoined(f, true);
        Assert.Equal(first + 3, f.Sent.Count); // actual rank/response/Append, no completed stats or perfection
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => IsVector(bytes, out _));
        Assert.DoesNotContain(f.Sent.Skip(first), bytes => LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
        Assert.NotNull(afterActualWrite); AssertUnchangedSaved(f, afterActualWrite!);
    }, mode: "pair");
}
