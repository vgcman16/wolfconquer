using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.FullRestoreConsumableFixture;

namespace WolfConquer.Tests;

public sealed partial class OwnedFullRestoreConsumableTests
{
    [Theory]
    [InlineData("invalid")] [InlineData("65536")] [InlineData("-1")]
    public Task InvalidStackMetadataDoesNotPublishResourcesItemsOrCooldown(string cap) => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files(); var mana = f.Player.Mana; int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); AssertVitals(f, 10, 40); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(mana, f.Player.Mana); Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
    }, cap: cap);

    [Fact]
    public Task NoImprovementWhenHpIsFullAndManaAlreadyAboveCapPreservesAllState() => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files(); var mana = f.Player.Mana; int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); AssertVitals(f, Maximum, 6000); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(mana, f.Player.Mana); Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
    }, initialHp: Maximum, initialMana: 6000);

    [Fact]
    public Task ActualCurrentCooldownRefusesBeforeAnyFullRestoreMutation() => Run(3, async f =>
    {
        await f.Login(); var original = f.Current;
        f.Store.Set(f.Connection, original with { LastLegacyPotionUseUtc = DateTimeOffset.UtcNow.AddMinutes(1) });
        var before = f.Current; var files = f.Files(); int first = f.Sent.Count; var mana = f.Player.Mana;
        await f.Use(); Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); AssertVitals(f, 10, 40);
        Assert.Equal(mana, f.Player.Mana); f.CheckFiles(files);
    });

    [Theory]
    [InlineData((ushort)3, true)] [InlineData((ushort)11, false)]
    public Task ActualDeadOwnerOrOverCapStackCannotConsume(ushort stack, bool dead) => Run(stack, async f =>
    {
        await f.Login(); var original = f.Current;
        if (dead) f.Store.Set(f.Connection, original with { Character = original.Character with { MinHitPoints = 0 } });
        var before = f.Current; var files = f.Files(); int first = f.Sent.Count; var mana = f.Player.Mana;
        await f.Use(); Assert.Same(before, f.Current); AssertVitals(f, dead ? 0 : 10, 40); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(mana, f.Player.Mana); Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task StaleCharacterOrDuplicatePhysicalUidRefusesWithoutOverwritingSavedEvidence(bool duplicate) => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var mana = f.Player.Mana;
        if (duplicate)
        {
            var bytes = File.ReadAllBytes(f.ItemsPath);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + PlayerItemSnapshotLoader.LegacyDbItemSize), Fixture.PotionUid);
            File.WriteAllBytes(f.ItemsPath, bytes);
        }
        else
            Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(f.CharacterPath, before.AccountId,
                new Dictionary<string, string> { ["MinMana"] = "41" }, true, out var reason), reason);
        var files = f.Files(); int first = f.Sent.Count; await f.Use();
        Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc); Assert.Equal(10, f.Player.HitPoints);
        Assert.Equal(mana, f.Player.Mana); Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
    });

    [Fact]
    public Task ActualTransportCloseRetiresTheFullRestoreOwnerBeforeReplay() => Run(3, async f =>
    {
        await f.Login(); var player = f.Player; var mana = player.Mana;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        await f.Connection.DisconnectAsync(); await f.Connection.CompleteTransportCloseAsync();
        Assert.True(binding.IsRetired); Assert.False(f.Store.TryGet(f.Connection, out _));
        var files = f.Files(); int first = f.Sent.Count; await f.Use();
        Assert.Equal(first, f.Sent.Count); Assert.Equal(10, player.HitPoints); Assert.Equal(mana, player.Mana); f.CheckFiles(files);
    });

    [Fact]
    public Task ProtectedUnverifiedPinRemainsAnAllowedRealConsumableOwner() => Run(3, async f =>
    {
        await f.Login(); Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection, f.Current, out var pin, out var verified));
        Assert.NotEqual(0U, pin.Password); Assert.False(verified); int first = f.Sent.Count;
        await f.Use(); Assert.Equal(4, f.Sent.Count - first); AssertVitals(f, Maximum, 5000);
        Assert.True(LegacySecondaryPasswordRuntime.TryGet(f.Connection, f.Current, out var retained, out verified));
        Assert.Equal(pin, retained); Assert.False(verified);
    }, protectedPin: true);

    [Fact]
    public Task PreparedFullRestoreWriterRejectsOtherScalarsPaidStateAndAnyResourceDecrease() => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files();
        var items = before.PlayerItems with { InventoryItems = before.PlayerItems.InventoryItems
            .Select(row => row.Uid == Fixture.PotionUid ? row.WithStackSize(2) : row).ToArray() };
        var full = new WorldPacketHandlers.BoundHitpointsInputs(before.Character,
            before.Character with { MinHitPoints = Maximum, MinMana = 5000 }).After;
        var forbidden = new[] { full with { Money = 999 }, full with { ConquerPoints = 201 }, full with { MaxMana = 6000 },
            full with { PaidNobility = new Dictionary<string, string> { ["forbidden"] = "1" } },
            full with { MinHitPoints = 0 }, full with { MinMana = 0 },
            full with { MinHitPoints = before.Character.MinHitPoints, MinMana = before.Character.MinMana } };
        foreach (var character in forbidden)
            Assert.False(LegacyFullRestoreConsumableWrite.TryPrepare(before, before with { Character = character, PlayerItems = items },
                f.Items, Fixture.PotionUid, out _, out _));
        Assert.Same(before, f.Current); AssertVitals(f, 10, 40); f.CheckFiles(files);
    });

    [WindowsTradeJournalFailureFact]
    public async Task ActualCharacterOrItemsReplacementFailureProvesPreimagesOrFencesWithoutAReply()
    {
        foreach (var character in new[] { true, false })
            await Run(3, async f =>
            {
                await f.Login(); var before = f.Current; var files = f.Files(); var player = f.Player; var mana = player.Mana;
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding); int first = f.Sent.Count;
                using (var held = new FileStream(character ? f.CharacterPath : f.ItemsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) await f.Use();
                Assert.Equal(first, f.Sent.Count); Assert.Equal(10, player.HitPoints); Assert.Equal(mana, player.Mana);
                if (LegacyTradeJournal.HasPending(f.Users))
                { Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired); await f.Use(); Assert.Equal(first, f.Sent.Count); }
                else
                {
                    Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc); Assert.False(binding.IsRetired);
                    Assert.False(f.Recovery.IsBlocked); f.CheckFiles(files); AssertVitals(f, 10, 40);
                    await f.Use(); AssertVitals(f, Maximum, 5000);
                }
            });
    }
}
