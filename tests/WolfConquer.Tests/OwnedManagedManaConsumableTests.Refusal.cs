using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedManagedManaConsumableTests
{
    [Fact]
    public Task MissingManaMetadataNeverPublishesCooldownManaItemsOrSuccess() => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files(); int first = f.Sent.Count; var mana = f.Player.Mana;
        await f.Use(); Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(first, f.Sent.Count); AssertManagedMana(f, 10); Assert.Equal(mana, f.Player.Mana); f.CheckFiles(files);
    }, metadata: false);

    [Theory]
    [InlineData("0")] [InlineData("-1")] [InlineData("4294967296")] [InlineData("missing")]
    public Task MalformedOrZeroManaMetadataIsARefusalBeforeCooldown(string amount) => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files(); int first = f.Sent.Count; var mana = f.Player.Mana;
        await f.Use(); Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(first, f.Sent.Count); AssertManagedMana(f, 10); Assert.Equal(mana, f.Player.Mana); f.CheckFiles(files);
    }, amount: amount);

    [Fact]
    public Task TheExistingFullManaNoChangeRuleDoesNotConsumeAUnitOrCooldown() => Run(3, async f =>
    {
        await f.Login(); // Genuine full-resource file input precedes owner/admission.
        var before = f.Current; var files = f.Files(); int first = f.Sent.Count; var mana = f.Player.Mana;
        await f.Use(); Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(first, f.Sent.Count); AssertManagedMana(f, 5000); Assert.Equal(mana, f.Player.Mana); f.CheckFiles(files);
    }, initialMana: 5000);

    [Fact]
    public Task ActualCurrentCooldownRefusesWithoutConsumingAnotherAuthorization() => Run(3, async f =>
    {
        await f.Login(); var original = f.Current;
        f.Store.Set(f.Connection, original with { LastLegacyPotionUseUtc = DateTimeOffset.UtcNow.AddMinutes(1) });
        var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); AssertManagedMana(f, 10); f.CheckFiles(files);
    });

    [Theory]
    [InlineData((ushort)3, true)] [InlineData((ushort)11, false)]
    public Task RealAdmittedZeroHpOrCatalogStackOverflowRefusesWithoutCooldownOrItemMutation(ushort stack, bool dead) => Run(stack, async f =>
    {
        await f.Login(); var original = f.Current;
        if (dead) f.Store.Set(f.Connection, original with { Character = original.Character with { MinHitPoints = 0 } });
        var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); AssertManagedMana(f, 10, dead ? 0 : 10); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
    });

    [Fact]
    public Task ActuallyRetiredAdmittedOwnerCannotConsumeByReusingItsOldConnection() => Run(3, async f =>
    {
        await f.Login(); var player = f.Player;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.True(f.Store.TryRemove(f.Connection, out _)); Assert.True(binding.IsRetired);
        var files = f.Files(); int first = f.Sent.Count;
        await f.Use(); Assert.Equal(first, f.Sent.Count); Assert.False(f.Store.TryGet(f.Connection, out _));
        Assert.Equal(10, player.HitPoints); f.CheckFiles(files);
    });

    [Fact]
    public Task ActualTransportCloseCannotBeReplayedAsAnOwnedManaUse() => Run(3, async f =>
    {
        await f.Login(); var player = f.Player; var mana = player.Mana;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        await f.Connection.DisconnectAsync(); await f.Connection.CompleteTransportCloseAsync();
        Assert.True(binding.IsRetired); Assert.False(f.Store.TryGet(f.Connection, out _));
        var files = f.Files(); int first = f.Sent.Count; await f.Use();
        Assert.Equal(first, f.Sent.Count); Assert.Equal(10, player.HitPoints); Assert.Equal(mana, player.Mana); f.CheckFiles(files);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task StaleCharacterOrDuplicatePhysicalUidRefusesBeforeAnyDebit(bool duplicate) => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var mana = f.Player.Mana;
        if (duplicate)
        {
            var bytes = File.ReadAllBytes(f.ItemsPath);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + PlayerItemSnapshotLoader.LegacyDbItemSize),
                WorldServerFrameHandlerTests.ManagedManaConsumableFixture.PotionUid);
            File.WriteAllBytes(f.ItemsPath, bytes);
        }
        else
            Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(f.CharacterPath, before.AccountId,
                new Dictionary<string, string> { ["MinMana"] = "11" }, true, out var reason), reason);
        var files = f.Files(); int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(10, f.Player.HitPoints); Assert.Equal(mana, f.Player.Mana); Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Fact]
    public Task ThePreparedManaWriterRejectsEveryNonManaScalarAndPaidChange() => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files();
        var items = before.PlayerItems with { InventoryItems = before.PlayerItems.InventoryItems
            .Select(row => row.Uid == WorldServerFrameHandlerTests.ManagedManaConsumableFixture.PotionUid ? row.WithStackSize(2) : row).ToArray() };
        var mana = new WorldPacketHandlers.BoundHitpointsInputs(before.Character, before.Character with { MinMana = 80 }).After;
        var forbidden = new[] { mana with { MinHitPoints = 80 }, mana with { MaxMana = 6000 }, mana with { Money = 999 },
            mana with { ConquerPoints = 201 }, mana with { BoundConquerPoints = 301 },
            mana with { PaidNobility = new Dictionary<string, string> { ["forbidden"] = "1" } } };
        foreach (var character in forbidden)
            Assert.False(LegacyManagedManaConsumableWrite.TryPrepare(before, before with { Character = character, PlayerItems = items },
                f.Items, WorldServerFrameHandlerTests.ManagedManaConsumableFixture.PotionUid, out _, out _));
        Assert.Same(before, f.Current); AssertManagedMana(f, 10); f.CheckFiles(files);
    });

    [WindowsTradeJournalFailureFact]
    public async Task RealWindowsCharacterAndItemsReplaceFailuresEitherProveBothPreimagesOrFenceTheOwner()
    {
        foreach (var lockCharacter in new[] { true, false })
            await Run(3, async f =>
            {
                await f.Login(); var before = f.Current; var files = f.Files(); var player = f.Player;
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding); int first = f.Sent.Count;
                using (var held = new FileStream(lockCharacter ? f.CharacterPath : f.ItemsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    await f.Use();
                Assert.Equal(first, f.Sent.Count); Assert.Equal(10, player.HitPoints);
                if (LegacyTradeJournal.HasPending(f.Users))
                {
                    Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
                    await f.Use(); Assert.Equal(first, f.Sent.Count);
                }
                else
                {
                    Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc); Assert.False(binding.IsRetired);
                    Assert.False(f.Recovery.IsBlocked); f.CheckFiles(files); AssertManagedMana(f, 10);
                    await f.Use(); AssertManagedMana(f, 80); // Real retry only after a proved definite refusal.
                }
            });
    }
}
