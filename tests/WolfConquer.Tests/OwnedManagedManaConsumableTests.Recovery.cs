using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.ManagedManaConsumableFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedManagedManaConsumableTests
{
    [Theory]
    [InlineData((int)LegacyManagedManaConsumableInterruption.AfterPending)]
    [InlineData((int)LegacyManagedManaConsumableInterruption.AfterCharacter)]
    [InlineData((int)LegacyManagedManaConsumableInterruption.AfterItems)]
    public Task RealV2InterruptionsFenceTheAdmittedOwnerAndFreshRecoveryFinishesBothImagesExactlyOnce(
        int boundary) => Run(3, async f =>
    {
        var interruption = (LegacyManagedManaConsumableInterruption)boundary;
        await f.Login(); var before = f.Current; var player = f.Player;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var characterBefore = File.ReadAllBytes(f.CharacterPath); var itemsBefore = File.ReadAllBytes(f.ItemsPath);
        var items = before.PlayerItems with { InventoryItems = before.PlayerItems.InventoryItems
            .Select(row => row.Uid == Fixture.PotionUid ? row.WithStackSize(2) : row).ToArray() };
        var character = new WorldPacketHandlers.BoundHitpointsInputs(before.Character, before.Character with { MinMana = 80 }).After;
        var updated = before with { Character = character, PlayerItems = items,
            LastLegacyPotionUseUtc = DateTimeOffset.UtcNow };
        Assert.True(LegacyManagedManaConsumableWrite.TryPrepare(before, updated, f.Items, Fixture.PotionUid, out var prepared, out var reason), reason);
        var writer = Assert.IsType<LegacyManagedManaConsumableWrite>(prepared); int calls = 0; int first = f.Sent.Count;
        Assert.Throws<LegacyCommittedSpellPublicationException>(() => f.Store.TryCommitSession(f.Connection, before, updated, () =>
        {
            calls++;
            Assert.Equal(LegacyManagedManaConsumableOutcome.RecoveryRequired, writer.TryCommit(out var failure, interruption));
            Assert.Contains("RecoveryRequired", failure, StringComparison.OrdinalIgnoreCase);
            f.Recovery.Block(failure);
            throw new LegacyCommittedSpellPublicationException(); // Actual uncertain WAL result, never an accepting saver.
        }));
        Assert.Equal(1, calls); Assert.Equal(first, f.Sent.Count); Assert.Equal(10, player.HitPoints);
        Assert.True(binding.IsRetired); Assert.True(f.Recovery.IsBlocked); Assert.True(LegacyTradeJournal.HasPending(f.Users));
        Assert.False(f.Store.TryGet(f.Connection, out _));
        if (interruption == LegacyManagedManaConsumableInterruption.AfterPending)
            Assert.Equal(characterBefore, File.ReadAllBytes(f.CharacterPath));
        if (interruption != LegacyManagedManaConsumableInterruption.AfterItems)
            Assert.Equal(itemsBefore, File.ReadAllBytes(f.ItemsPath));
        await f.Use(); Assert.Equal(first, f.Sent.Count); // Old live owner cannot replay a pending use.

        await f.CloseOwnerForRecovery(); // End the actual old transport/registry owner before fresh startup.
        var fresh = new LegacyTradeRecoveryState(f.Users, f.Items);
        Assert.True(fresh.TryEnsureInitialized(out reason), reason);
        Assert.False(fresh.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        Assert.Equal(LegacyBoundHitpointsScalarWrite.ProjectAfterImage(characterBefore, updated.Character), File.ReadAllBytes(f.CharacterPath));
        Assert.Equal(ExpectedItems(itemsBefore, 3), File.ReadAllBytes(f.ItemsPath));
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal(80L, saved.MinMana); Assert.Equal(5000L, saved.MaxMana); Assert.Equal(10L, saved.MinHitPoints);
        var recovered = new PlayerItemSnapshotLoader(f.Items).LoadByCharacterUid(Fixture.Uid);
        Assert.Equal((ushort)2, Assert.Single(recovered.InventoryItems, row => row.Uid == Fixture.PotionUid).StackSize);
        var completed = f.Files(); Assert.True(fresh.TryEnsureInitialized(out reason), reason); f.CheckFiles(completed);
        Assert.Equal(10, player.HitPoints); Assert.True(binding.IsRetired); Assert.False(f.Store.TryGet(f.Connection, out _));
    });

    [Fact]
    public Task ActualTransportFaultAfterBothDurableImagesCannotRefundOrConsumeAgain() => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var player = f.Player;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var original = File.ReadAllBytes(f.ItemsPath); int first = f.Sent.Count, attempts = 0;
        Exception? assertionFailure = null;
        f.OnSend = sentFrame =>
        {
            attempts++;
            try { AssertManagedMana(f, 80); Assert.Equal(ExpectedItems(original, 3), File.ReadAllBytes(f.ItemsPath)); }
            catch (Exception error) { assertionFailure = error; throw; }
            throw new IOException("Owned deliberate managed Mana notification failure.");
        };
        await f.Use(); f.OnSend = null;
        if (assertionFailure is not null) ExceptionDispatchInfo.Capture(assertionFailure).Throw();
        Assert.Equal(1, attempts); Assert.Equal(first, f.Sent.Count); Assert.Equal(10, player.HitPoints);
        Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        var files = f.Files(); await f.Use(); Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
        Assert.Equal((ushort)2, Assert.Single(new PlayerItemSnapshotLoader(f.Items).LoadByCharacterUid(Fixture.Uid)
            .InventoryItems, row => row.Uid == Fixture.PotionUid).StackSize);
        Assert.Equal(before.Character.Money, f.Current.Character.Money);
    });
}
