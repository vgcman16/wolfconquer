using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedHealthConsumableTests
{
    [Fact]
    public Task MissingHealthMetadataNeverPublishesCooldownHpItemsOrSuccess() => Run(3, async f =>
    {
        await f.Login(); var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(first, f.Sent.Count); AssertHp(f, 10); f.CheckFiles(files);
    }, metadata: false);

    [Fact]
    public Task ActualCurrentCooldownRefusesWithoutConsumingAnotherAuthorization() => Run(3, async f =>
    {
        await f.Login(); var original = f.Current;
        f.Store.Set(f.Connection, original with { LastLegacyPotionUseUtc = DateTimeOffset.UtcNow.AddMinutes(1) });
        var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); AssertHp(f, 10); f.CheckFiles(files);
    });

    [Theory]
    [InlineData((ushort)3, true)] [InlineData((ushort)11, false)]
    public Task RealAdmittedZeroHpOrCatalogStackOverflowRefusesWithoutCooldownOrItemMutation(ushort stack, bool dead) => Run(stack, async f =>
    {
        await f.Login(); var original = f.Current;
        if (dead) f.Store.Set(f.Connection, original with { Character = original.Character with { MinHitPoints = 0 } });
        var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); AssertHp(f, dead ? 0 : 10); Assert.Null(f.Current.LastLegacyPotionUseUtc);
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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task StaleCharacterOrDuplicatePhysicalUidRefusesBeforeAnyDebit(bool duplicate) => Run(3, async f =>
    {
        await f.Login(); var before = f.Current;
        if (duplicate)
        {
            var bytes = File.ReadAllBytes(f.ItemsPath);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + PlayerItemSnapshotLoader.LegacyDbItemSize),
                WorldServerFrameHandlerTests.HealthConsumableFixture.PotionUid);
            File.WriteAllBytes(f.ItemsPath, bytes);
        }
        else
            Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(f.CharacterPath, before.AccountId,
                new Dictionary<string, string> { ["MinHitPoints"] = "11" }, true, out var reason), reason);
        var files = f.Files(); int first = f.Sent.Count;
        await f.Use(); Assert.Same(before, f.Current); Assert.Null(f.Current.LastLegacyPotionUseUtc);
        Assert.Equal(10, f.Player.HitPoints); Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
        Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
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
                    Assert.False(f.Recovery.IsBlocked); f.CheckFiles(files); AssertHp(f, 10);
                    await f.Use(); AssertHp(f, 80); // Real retry only after a proved definite refusal.
                }
            });
    }
}
