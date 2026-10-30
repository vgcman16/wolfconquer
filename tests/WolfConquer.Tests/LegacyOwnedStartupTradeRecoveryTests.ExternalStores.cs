using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyOwnedStartupTradeRecoveryFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupTradeRecoveryTests
{
    [Theory]
    [InlineData("detained-duplicate")]
    [InlineData("detained-unregistered")]
    [InlineData("mail-unregistered")]
    public Task CapturedPairScanStillChecksUnrelatedDetainedItemsAndMailboxReferences(string fault) =>
        WithFixture(async f =>
        {
            // This genuine producer prefix physically duplicates the moved UID.
            // Its closed logical pair view must not exempt another store.
            await f.StageAsync(3);
            await f.CloseAsync();
            var uid = fault == "detained-duplicate" ? Fixture.LeftItemUid :
                LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1;
            if (fault == "mail-unregistered")
            {
                // Serialize through the real mailbox writer in an unrelated
                // owned seed root, then introduce it as corrupt recovery input.
                var seed = new LegacyMailboxStore(Path.Combine(f.Data.Root, "external-mail-seed"));
                seed.Add(Fixture.LeftUid, new LegacyMailboxEntry
                {
                    Id = 1, Title = "Unregistered recovery reference", ItemId = uid,
                    Timer = DateTime.Now.AddDays(1).Ticks,
                });
                seed.Save(Fixture.LeftUid);
                File.WriteAllBytes(Path.Combine(f.Data.Mailbox, Fixture.LeftUid + ".bin"),
                    File.ReadAllBytes(seed.GetMailboxPath(Fixture.LeftUid)));
            }
            else
            {
                Assert.Equal(322, Unsafe.SizeOf<LegacyNativeMsgDetainedItem>());
                var row = new LegacyNativeMsgDetainedItem
                {
                    UID = 1, ItemUID = uid, ItemID = Fixture.ItemId,
                    OwnerUID = Fixture.LeftUid, GainerUID = Fixture.RightUid,
                    Action = LegacyNativeMsgDetainedItem.ContainerType.ClaimPage,
                };
                var bytes = new byte[sizeof(int) + Unsafe.SizeOf<LegacyNativeMsgDetainedItem>()];
                BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
                MemoryMarshal.Write(bytes.AsSpan(sizeof(int)), in row);
                File.WriteAllBytes(f.Data.Redeem, bytes);
                File.WriteAllBytes(f.Data.Claim, bytes);
            }
            // AssertRefused compares all owned files, including the genuine
            // pending record, both physical targets and these foreign inputs.
            f.AssertRefused();
        });
}
