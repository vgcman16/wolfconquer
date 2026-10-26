using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Fact]
    public async Task ActualD13StandaloneMailboxPackagePreservesRealMainPinAdmissionAndEveryReward()
    {
        const uint uid = 1_299_812;
        const uint mailId = 32;
        Seed(uid, "111,0,0");
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid, SashPinSetupItems(deposited: false));
        var mailboxes = new LegacyMailboxStore(root);
        mailboxes.Add(uid, new LegacyMailboxEntry
        {
            Id = mailId, ItemId = 40_006, Money = 9, EMoney = 3, Flag = 1,
            Title = "PIN package", SenderName = "System", Content = "Keep the complete reward.",
            Timer = new DateTime(2099, 1, 1).Ticks,
        });
        mailboxes.Save(uid);
        var owner = NewOwner();
        Player? player = null;
        var errors = new List<Exception>();
        try
        {
            player = await Login(owner, uid);
            var before = Current(owner, player);
            var json = JsonSerializer.Serialize(before);
            var files = SashPinFiles();
            var first = player.Sent.Count;
            var request = new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimRecordType, mailId, []).ToFrameBytes();
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
            Assert.False(player.NativeClient.Player.IsCheckedPass);

            AssertReply(await Password(owner, player, 1, newPin: 111), 5, 257);
            Assert.True(player.NativeClient.Player.IsCheckedPass);
            AssertSashPinFiles(files);
            before = Current(owner, player);
            json = JsonSerializer.Serialize(before);
            first = player.Sent.Count;

            await SashPinFrameAsync(owner, player, request, calls);

            Assert.Same(before, Current(owner, player));
            Assert.Equal(json, JsonSerializer.Serialize(before));
            AssertSashPinFiles(files);
            Assert.Equal(first, player.Sent.Count);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(player.Connection, before));
            Assert.True(player.NativeClient.Player.IsCheckedPass);
            Assert.Equal(1U, Assert.Single(new LegacyMailboxStore(root).Load(uid)).Flag);
            Assert.Contains("mailboxClaimPackageOwnersUnresolved", owner.Log.ToString());
            Assert.All(calls, task => Assert.True(task.IsCompletedSuccessfully));
        }
        catch (Exception error) { errors.Add(error); }
        if (player is not null)
        {
            try { await player.Connection.CompleteTransportCloseAsync(); }
            catch (Exception error) { errors.Add(error); }
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
}
