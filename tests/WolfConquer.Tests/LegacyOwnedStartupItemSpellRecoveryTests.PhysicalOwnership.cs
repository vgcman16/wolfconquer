using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupItemSpellRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupItemSpellRecoveryTests
{
    [Theory]
    [InlineData("unrelated-items")] [InlineData("redeem-physical")]
    [InlineData("claim-physical")] [InlineData("detained-alias")]
    public Task AllAfterBookRetirementCannotRelinquishItsCapturedBeforeUidToAnotherPhysicalOwner(string owner) => WithFixture(async f =>
    {
        await f.StageAsync(3); await f.CloseAsync(); RequireCapturedBookAlreadyConsumed(f);
        if (owner == "unrelated-items")
            File.WriteAllBytes(Path.Combine(f.Data.Items, (Fixture.Uid + 1).ToString(CultureInfo.InvariantCulture) + ".bin"),
                PlayerItemSnapshotLoader.ToLegacyBytes(new([NewItem(Fixture.ItemUid)], [], [], [], [])));
        else
        {
            var row = DetainedImage(Fixture.ItemUid, rewardOnly: false);
            if (owner is "redeem-physical" or "detained-alias") File.WriteAllBytes(f.Data.Redeem, row);
            if (owner is "claim-physical" or "detained-alias") File.WriteAllBytes(f.Data.Claim, row);
        }
        // This is deliberately unique in the current physical namespace: the
        // consumed book is absent from every actual AFTER target. Only the
        // captured BEFORE ownership proof reveals the conflicting custody.
        Assert.True(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem, f.Data.Claim,
            out var observed, out var reason), reason);
        Assert.Equal(4, observed.PhysicalCount); Assert.True(observed.Maximum <= LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor);
        f.AssertRefused();
    });

    [Theory]
    [InlineData("no-reference")] [InlineData("mail-reference")] [InlineData("reward-reference")]
    public Task CapturedAllAfterBookRetirementAllowsUnownedExternalReferencesWithoutCreatingPhysicalOwnership(string reference) =>
        WithFixture(async f =>
        {
            await f.StageAsync(3); await f.CloseAsync(); RequireCapturedBookAlreadyConsumed(f);
            string? referencePath = null;
            if (reference == "mail-reference") referencePath = WriteRetiredBookMailReference(f, Fixture.ItemUid);
            else if (reference == "reward-reference")
            {
                referencePath = f.Data.Redeem; File.WriteAllBytes(referencePath, DetainedImage(Fixture.ItemUid, rewardOnly: true));
            }
            var referenceBytes = referencePath is null ? null : File.ReadAllBytes(referencePath);
            Assert.True(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem, f.Data.Claim,
                out var observed, out var reason), reason);
            Assert.Equal(3, observed.PhysicalCount); Assert.Equal(referencePath is null ? 0 : 1, observed.ReferenceCount);
            f.Open(); f.AssertRecoveredImages();
            if (referencePath is not null) Assert.Equal(referenceBytes, File.ReadAllBytes(referencePath));
            await f.CloseAsync(); var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        });

    [Fact]
    public Task RetiredBookReferenceExemptionCannotBypassTheActualPinnedHighWater() => WithFixture(async f =>
    {
        await f.StageAsync(3); await f.CloseAsync(); RequireCapturedBookAlreadyConsumed(f);
        WriteRetiredBookMailReference(f, LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1);
        Assert.True(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem, f.Data.Claim,
            out var observed, out var reason), reason);
        Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1, observed.Maximum);
        Assert.Equal(3, observed.PhysicalCount); Assert.Equal(1, observed.ReferenceCount); f.AssertRefused();
    });

    private static void RequireCapturedBookAlreadyConsumed(Fixture fixture)
    {
        foreach (var (path, after) in fixture.After) Assert.Equal(after, File.ReadAllBytes(path));
        Assert.Contains(Fixture.ItemUid, LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(fixture.Before[fixture.ItemsPath]!));
        Assert.DoesNotContain(Fixture.ItemUid, LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(fixture.After[fixture.ItemsPath]));
        Assert.True(LegacyTradeJournal.HasPending(fixture.Data.Users));
    }

    private static string WriteRetiredBookMailReference(Fixture fixture, uint itemUid)
    {
        var mailbox = new LegacyMailboxStore(fixture.Data.Root); const uint owner = Fixture.Uid + 1;
        mailbox.Add(owner, new LegacyMailboxEntry
        { Id = 1, Title = "owned retired UID reference", ItemId = itemUid, Timer = DateTime.Now.AddDays(1).Ticks });
        mailbox.Save(owner); return mailbox.GetMailboxPath(owner);
    }

    private static byte[] DetainedImage(uint itemUid, bool rewardOnly)
    {
        Assert.Equal(322, Unsafe.SizeOf<LegacyNativeMsgDetainedItem>());
        var row = new LegacyNativeMsgDetainedItem
        {
            UID = 1, ItemUID = itemUid, ItemID = Fixture.BookType, OwnerUID = Fixture.Uid, GainerUID = Fixture.Uid + 1,
            Action = rewardOnly ? LegacyNativeMsgDetainedItem.ContainerType.RewardCps : LegacyNativeMsgDetainedItem.ContainerType.ClaimPage,
        };
        var bytes = new byte[sizeof(int) + Unsafe.SizeOf<LegacyNativeMsgDetainedItem>()];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1); MemoryMarshal.Write(bytes.AsSpan(sizeof(int)), in row); return bytes;
    }
}
