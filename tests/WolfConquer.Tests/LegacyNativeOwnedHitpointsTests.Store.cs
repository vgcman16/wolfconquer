using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedHitpointsTests
{
    [Fact]
    public Task RealFrameSetAndCasEachPersistTheirOwnCurrentHpImageBeforeReturning() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); AssertHp(f, 1000);
        var first = f.Current;
        f.Store.Set(f.Connection, first with { Character = first.Character with { MinHitPoints = 777 } });
        AssertHp(f, 777);
        var second = f.Current;
        Assert.True(f.Store.TryCompareAndSet(f.Connection, second,
            second with { Character = second.Character with { MinHitPoints = 0 } }, out var final));
        Assert.Same(f.Current, final); AssertHp(f, 0);
        var third = f.Current;
        f.Store.Set(f.Connection, third with { Character = third.Character with { MinHitPoints = 333 } });
        AssertHp(f, 333);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Theory]
    [InlineData(-2_147_483_649L)]
    [InlineData(2_147_483_648L)]
    public Task UnrepresentableRuntimeHpRefusesBeforeEitherOwnerOrSavedFilesChange(long hp) => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear();
        var before = f.Current; var files = f.Files();
        var proposed = before with { Character = before.Character with { MinHitPoints = hp } };

        var failure = Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, proposed));
        Assert.Equal("ordinaryHitpointsSignedRangeInvalid", failure.Message);

        Assert.Same(before, f.Current); AssertHp(f, 1000); f.RequireFiles(files);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked);
    });

    [WindowsTradeJournalFailureFact]
    public Task ActualWindowsCharacterLockRefusesSetAndCasWithoutAdvancingManagedOrPhysicalHp() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear();
        var before = f.Current; var files = f.Files(); var binding = Binding(f);
        var proposed = before with { Character = before.Character with { MinHitPoints = 555 } };
        // The file remains readable for genuine owner/image validation. Windows
        // denies the atomic writer's replacement while this real handle is open.
        using (var locked = new FileStream(f.CharacterPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Throws<LegacyNativeHitpointsRefusalException>(() => f.Store.Set(f.Connection, proposed));
            Assert.Same(before, f.Current); AssertHp(f, 1000);
            Assert.False(f.Store.TryCompareAndSet(f.Connection, before, proposed, out var refused));
            Assert.Null(refused); Assert.Same(before, f.Current); AssertHp(f, 1000);
            f.RequireFiles(files); Assert.Empty(f.Sent); Assert.False(binding.IsRetired);
            Assert.False(f.Recovery.IsBlocked);
        }
        // A definite refusal leaves the original issuance valid for a real retry.
        Assert.True(f.Store.TryCompareAndSet(f.Connection, before, proposed, out var final));
        Assert.Same(final, f.Current); Assert.Same(binding, Binding(f)); AssertHp(f, 555);
        Assert.Empty(f.Sent);
    });

    [Fact]
    public Task StaleOrRetiredRecordsCannotAcquirePhysicalHpAuthorityByReusingTheSameUid() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear();
        var before = f.Current; var binding = Binding(f);
        f.Store.Set(f.Connection, before with { ActiveNpcId = 17 });
        var current = f.Current; var files = f.Files();
        Assert.False(f.Store.TryCompareAndSet(f.Connection, before,
            before with { Character = before.Character with { MinHitPoints = 444 } }));
        Assert.Same(current, f.Current); AssertHp(f, 1000); f.RequireFiles(files);
        Assert.True(f.Store.TryRemove(f.Connection, out var removed)); Assert.Same(current, removed);
        Assert.True(binding.IsRetired); Assert.Same(binding, f.Native.OrdinarySpellBinding);
        // Retained positive storage is historical state, not a revived owner.
        Assert.Equal(1000, f.Player.HitPoints); Assert.Null(f.CurrentOrNull);
        Assert.ThrowsAny<InvalidOperationException>(() => f.Store.Set(f.Connection,
            current with { Character = current.Character with { MinHitPoints = 444 } }));
        Assert.Equal(1000, f.Player.HitPoints); Assert.Null(f.CurrentOrNull);
        f.RequireFiles(files); Assert.Empty(f.Sent);
    });

    [Fact]
    public Task ActualNativeSpellOnlyPublicationPreservesHydratedHpAndTheSavedCharacter() => Run(async f =>
    {
        await f.LoginAsync(); AssertHp(f, 1000); f.Sent.Clear();
        var character = File.ReadAllBytes(f.CharacterPath);
        await NativePacketTestThread.Run(() =>
        {
            using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
            var storage = Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells);
            storage.Add(packet, 6002, 1);
            Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6002);
            AssertHp(f, 1000);
        });
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
        Assert.Single(f.Sent); Assert.True(LegacySpellPacket.TryParse(
            OwnedFrame(f.Sent[0]), out var added)); Assert.Equal(6002, added.Id);
    });

    [Fact]
    public Task UnchangedHpDoesNotSuppressTheConcreteFrameManaPersistencePostlude() => Run(async f =>
    {
        await f.LoginAsync(); f.Sent.Clear(); AssertHp(f, 1000);
        var before = f.Current;
        var mana = checked(before.Character.MinMana + 3);

        Assert.True(f.Store.TryCompareAndSet(f.Connection, before,
            before with { Character = before.Character with { MinMana = mana } }, out var final));

        Assert.Same(final, f.Current); AssertHp(f, 1000);
        var saved = Assert.IsType<CharacterSnapshot>(new CharacterSnapshotLoader(f.Users)
            .TryLoadByAccountIdForAdmission(FixtureUid).Snapshot);
        Assert.Equal(mana, saved.MinMana); Assert.Equal(mana, f.Current.Character.MinMana);
        Assert.Empty(f.Sent); Assert.False(f.Recovery.IsBlocked);
    });

    private static WolfConquer.Core.Packets.PacketFrame OwnedFrame(byte[] bytes)
    {
        Assert.True(WolfConquer.Core.Packets.PacketFrameDecoder.TryRead(
            new System.Buffers.ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
