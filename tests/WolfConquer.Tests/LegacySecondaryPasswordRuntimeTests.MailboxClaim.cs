using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Fact]
    public async Task ActualD13MailboxClaimRequiresMainPinIssuerWithoutConsumingRejectedAttachment()
    {
        const uint uid = 1_299_811;
        const uint mailId = 31;
        const uint attachment = 40_006;
        Seed(uid, "111,0,0");
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, SashPinSetupItems(deposited: false));
        var mailboxes = new LegacyMailboxStore(root);
        mailboxes.Add(uid, new LegacyMailboxEntry
        {
            Id = mailId, ItemId = attachment, Money = 9, EMoney = 3, Title = "Main PIN attachment",
            SenderName = "System", Content = "Preserve currency and raw bytes", Timer = new DateTime(2099, 1, 1).Ticks,
        });
        mailboxes.Save(uid);
        var owner = NewOwner();
        Action<byte[]>? witness = null;
        Player? player = null;
        var errors = new List<Exception>();
        try
        {
            player = await Login(owner, uid, beforeSend: frame => witness?.Invoke(frame));
            var before = Current(owner, player);
            var json = JsonSerializer.Serialize(before);
            var files = SashPinFiles();
            var first = player.Sent.Count;
            var request = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimItem, mailId, []).ToFrameBytes();
            var calls = new List<Task>();
            Assert.False(player.NativeClient.Player.IsCheckedPass);

            await SashPinFrameAsync(owner, player, request, calls);

            Assert.Same(before, Current(owner, player));
            Assert.Equal(json, JsonSerializer.Serialize(before));
            AssertSashPinFiles(files);
            Assert.Equal(first, player.Sent.Count);
            Assert.False(player.NativeClient.Player.IsCheckedPass);
            AssertReply(await Password(owner, player, 1, newPin: 999), 6, 0);
            first = player.Sent.Count;
            await SashPinFrameAsync(owner, player, request, calls);
            Assert.Equal(first, player.Sent.Count);
            AssertSashPinFiles(files);
            AssertReply(await Password(owner, player, 1, newPin: 111), 5, 257);
            Assert.True(player.NativeClient.Player.IsCheckedPass);
            AssertSashPinFiles(files);

            var incoming = Assert.Single(before.PlayerItems.WarehouseItems, row => row.Uid == attachment).WithWarehouseId(0, 0);
            var expectedItems = before.PlayerItems with
            {
                InventoryItems = before.PlayerItems.InventoryItems.Append(incoming).OrderBy(row => row.Uid).ToArray(),
                WarehouseItems = before.PlayerItems.WarehouseItems.Where(row => row.Uid != attachment).ToArray(),
            };
            var expectedFiles = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetRelativePath(root, ItemPath(uid))] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems),
            };
            var mailPath = Path.GetRelativePath(root, mailboxes.GetMailboxPath(uid));
            var mailBytes = files[mailPath].ToArray();
            Assert.Equal(4 + 740, mailBytes.Length);
            mailBytes.AsSpan(4 + 720, 4).Clear();
            expectedFiles[mailPath] = mailBytes;
            var observed = 0;
            witness = _ => { AssertSashPinFiles(expectedFiles); observed++; };
            first = player.Sent.Count;

            await SashPinFrameAsync(owner, player, request, calls);

            witness = null;
            Assert.Equal(2, observed);
            AssertSashPinFiles(expectedFiles);
            var committed = Current(owner, player);
            Assert.Equal(before with { PlayerItems = committed.PlayerItems }, committed);
            Assert.Equal(JsonSerializer.Serialize(expectedItems), JsonSerializer.Serialize(committed.PlayerItems));
            Assert.Equal(json, JsonSerializer.Serialize(before));
            var frames = player.Sent.Skip(first).ToArray();
            Assert.Equal(2, frames.Length);
            Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(incoming).ToFrameBytes(), frames[0]);
            Assert.Equal(request, frames[1]);
            first = player.Sent.Count;
            await SashPinFrameAsync(owner, player, request, calls);
            Assert.Equal(request, Assert.Single(player.Sent.Skip(first)));
            Assert.Same(committed, Current(owner, player));
            AssertSashPinFiles(expectedFiles);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, committed));
            Assert.All(calls, task => Assert.True(task.IsCompletedSuccessfully));
        }
        catch (Exception error) { errors.Add(error); }
        witness = null;
        if (player is not null)
        {
            try { await player.Connection.CompleteTransportCloseAsync(); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
}
