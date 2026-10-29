using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeSpellJoinFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeOwnedVitalsTests
{
    private static Task Run(Func<Fixture, Task> body, long? mana = null, long? maximum = null, string? opaque = null) =>
        WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(body, lineage: false,
            initialMana: mana, maximumMana: maximum, extraCharacterLines: opaque);
    private static LegacyOrdinarySpellBinding Binding(Fixture f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.Same(f.Store, binding.Store); Assert.Same(f.Connection, binding.Connection);
        Assert.Same(f.Native, binding.Client); Assert.Same(f.Player, binding.Player); return binding;
    }
    private static CharacterSnapshot Saved(Fixture f) => Assert.IsType<CharacterSnapshot>(
        new CharacterSnapshotLoader(f.Users).TryLoadByAccountIdForAdmission(Fixture.Uid).Snapshot);
    private static void Vitals(Fixture f, int hp, uint mana)
    {
        Assert.Equal((long)hp, f.Current.Character.MinHitPoints); Assert.Equal(hp, f.Player.HitPoints);
        Assert.Equal((long)mana, f.Current.Character.MinMana); Assert.Equal(mana, f.Player.Mana);
        var saved = Saved(f); Assert.Equal((long)hp, saved.MinHitPoints); Assert.Equal((long)mana, saved.MinMana);
        var binding = Binding(f); Assert.False(binding.IsRetired); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
    }
    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
    }
    private static LegacyTradeJournalChange[] Changes(Fixture f, byte[] before, byte[] after)
    {
        var spells = File.ReadAllBytes(f.SpellPath);
        return [new(LegacyTradeJournalFileKind.Character, f.Current.AccountId, before, after),
            new(LegacyTradeJournalFileKind.Spells, f.Current.Character.Uid, spells, spells)];
    }

    [Theory]
    [InlineData(0L)] [InlineData(65535L)] [InlineData(65536L)] [InlineData(4294967295L)]
    public Task GenuineAdmissionPublishesSavedCurrentManaBeforeSuccessWithoutNarrowingUintMaximum(long mana) => Run(async f =>
    {
        var player = f.Player; int observations = 0;
        var success = LegacyMsgMessagePacket.AnswerOk().ToFrameBytes();
        f.ObserveSend = (bytes, _) =>
        {
            if (bytes.AsSpan().SequenceEqual(success))
            {
                observations++; Vitals(f, 1000, checked((uint)mana));
                Assert.Equal((long)uint.MaxValue, f.Current.Character.MaxMana);
                Assert.Equal((long)uint.MaxValue, Saved(f).MaxMana);
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            }
            f.Sent.Add(bytes); return ValueTask.CompletedTask;
        };
        await f.LoginAsync(); f.RequireCommitObserved(); Assert.Equal(1, observations);
        Assert.Same(player, f.Player); Vitals(f, 1000, checked((uint)mana));
        var current = f.Current; var files = f.Files(); int count = f.Sent.Count;
        await f.LoginAsync(); Assert.Same(current, f.Current); Assert.Equal(count, f.Sent.Count); f.RequireFiles(files);
        Assert.False(f.Recovery.IsBlocked); Assert.DoesNotContain("runtime exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
    }, mana, uint.MaxValue);

    [Theory]
    [InlineData(-1L)] [InlineData(4294967296L)]
    public Task SavedCurrentManaOutsideUintRefusesBeforeAnyNativeOrDurableAdmission(long mana) => Run(async f =>
    {
        // Corrupt a genuine owned fixture's saved input before admission. The
        // issuer itself was opened from valid data; no invalid-owner prerequisite.
        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(f.CharacterPath, Fixture.Uid,
            new Dictionary<string, string> { ["MinMana"] = mana.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            detailedIoFailures: true, out var reason), reason);
        var files = f.Files(); var player = f.Player; var hp = player.HitPoints; var nativeMana = player.Mana;
        await f.LoginAsync();
        Assert.Null(f.CurrentOrNull); Assert.Null(f.Native.OrdinarySpellBinding); Assert.Same(player, f.Player);
        Assert.Equal(0u, player.UID); Assert.False(player.CompleteLogin);
        Assert.Equal(hp, player.HitPoints); Assert.Equal(nativeMana, player.Mana);
        Assert.Empty(f.Sent); f.RequireFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Fact]
    public Task ActualSetAndCasPublishBothVitalsFromOneCompleteOpaqueCharacterImage() => Run(async f =>
    {
        await f.LoginAsync(); f.RequireCommitObserved(); f.Sent.Clear(); var binding = Binding(f);
        var before = f.Current; var original = File.ReadAllBytes(f.CharacterPath);
        var target = before.Character with { MinHitPoints = 777, MinMana = 123 };
        var image = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(original, target);
        f.Store.Set(f.Connection, before with { Character = target });
        Vitals(f, 777, 123); Assert.Equal(image, File.ReadAllBytes(f.CharacterPath));
        Assert.Equal("123", f.Current.Character.Attributes["MinMana"]);
        var second = f.Current;
        Assert.True(f.Store.TryCompareAndSet(f.Connection, second,
            second with { Character = second.Character with { MinHitPoints = 0, MinMana = uint.MaxValue } }, out var final));
        Assert.Same(final, f.Current); Vitals(f, 0, uint.MaxValue); Assert.Same(binding, Binding(f));
        Assert.Equal("4294967295", f.Current.Character.Attributes["MinMana"]);
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _);
        Assert.Contains("; owned vitals opaque", text); Assert.Contains("OwnedVitalsOpaque=retain", text);
        Assert.Equal(before.Character.MaxMana, f.Current.Character.MaxMana);
        Assert.Equal(before.Character.Money, f.Current.Character.Money); Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
    }, opaque: "; owned vitals opaque\r\nOwnedVitalsOpaque=retain");

    [Theory]
    [InlineData(-1L)] [InlineData(4294967296L)]
    public Task UnrepresentableManagedCurrentManaCannotCommitOrPartiallyChangeHp(long mana) => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); var before = f.Current; var files = f.Files();
        var failure = Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection,
            before with { Character = before.Character with { MinHitPoints = 555, MinMana = mana } }));
        Assert.Equal("ordinaryManaUnsignedRangeInvalid", failure.Message);
        Assert.Same(before, f.Current); Vitals(f, 1000, 5000); f.RequireFiles(files);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked);
    });
}
