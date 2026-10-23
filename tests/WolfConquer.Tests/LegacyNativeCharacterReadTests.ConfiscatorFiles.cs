using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    [Fact]
    public Task RawDetainedIoRetainsCallerBytesAcrossShortReadAndDoesNotMarshalBoolWidth() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); string path = Path.Combine(scope.OwnedRoot!, "owned-detained.bin");
        var input = Detained(); input.Bound = true; input.Enchant = 0x7F;
        var file = new LegacyNativeItemRecordFile(); Assert.True(file.Open(path, FileMode.Create));
        try { Assert.True(file.WriteDetained(ref input)); } finally { file.Close(); }
        Assert.Equal(Raw(input), File.ReadAllBytes(path));
        Assert.True(file.Open(path, FileMode.Open)); var output = default(LegacyNativeMsgDetainedItem);
        try { Assert.True(file.ReadDetained(ref output)); Assert.False(file.ReadDetained(ref output)); } finally { file.Close(); }
        Assert.Equal(Raw(input), Raw(output)); Assert.True(output.Bound); Assert.Equal((byte)127, output.Enchant);
        File.WriteAllBytes(path, new byte[] { 3,0,0,0 });
        Assert.True(file.Open(path, FileMode.Open)); try { Assert.False(file.ReadDetained(ref output)); } finally { file.Close(); }
        Assert.Equal(3u, output.UID); Assert.Equal(71u, output.ItemUID); Assert.True(output.Bound); Assert.Equal("Signature", output.Signature);
        var unopened = new LegacyNativeItemRecordFile();
        Assert.Throws<IOException>(() => unopened.ReadDetained(ref output)); Assert.Throws<IOException>(() => unopened.WriteDetained(ref output));
    });

    [Fact]
    public Task FullContainerSaveAndLoadReadBothOwnedFilesWithSourceDaysAndCounterRules() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.OwnFiles(); var table = new LegacyNativeConfiscatorTable();
        var owner = new LegacyNativeConfiscatorRuntime(); var normal = Detained(30); normal.Bound = true; normal.Enchant = 127;
        owner.RedeemContainer[30] = normal; var reward = Detained(20); reward.Action = LegacyNativeMsgDetainedItem.ContainerType.RewardCps;
        owner.ClaimContainer[20] = reward; table.QueueObj(17, owner); table.Save();
        byte[] redeem = File.ReadAllBytes(Path.Combine(scope.OwnedRoot!, "RedeemContainer.bin"));
        Assert.Equal(326, redeem.Length); Assert.Equal(1u, U32(redeem, 0)); Assert.Equal(Raw(normal), redeem.AsSpan(4).ToArray());
        var loaded = new LegacyNativeConfiscatorTable(); loaded.Load();
        Assert.Equal(31u, LegacyNativeConfiscatorRuntime.CounterUID.Count);
        var loadedNormal = loaded.PollContainers[17].RedeemContainer[30]; Assert.True(loadedNormal.Bound); Assert.Equal((byte)127, loadedNormal.Enchant);
        Assert.Equal(0u, loadedNormal.DaysLeft); Assert.Equal(30u, loaded.PollContainers[33].ClaimContainer[30].UID);
        Assert.Equal(20u, loaded.PollContainers[33].ClaimContainer[20].UID);
    });

    [Fact]
    public void ContainerQueueDoesNotReplaceAndDeleteRetainsNativeWrongClaimRemovalTarget()
    {
        var table = new LegacyNativeConfiscatorTable(); var first = new LegacyNativeConfiscatorRuntime();
        var second = new LegacyNativeConfiscatorRuntime(); table.QueueObj(17, first); table.QueueObj(17, second);
        Assert.Same(first, table.PollContainers[17]); table.QueueObj(33, second);
        second.RedeemContainer[100] = Detained(100); second.RedeemContainer[101] = Detained(101, 99, 88);
        second.ClaimContainer[101] = Detained(101); table.DeleteAllOf(17);
        Assert.False(table.PollContainers.ContainsKey(17)); Assert.Empty(second.RedeemContainer);
        Assert.Single(second.ClaimContainer); // Native claim loop removed RedeemContainer, not ClaimContainer.
        second.RedeemContainer[102] = Detained(102, 999); table.DeleteAllOf(999);
        Assert.True(second.RedeemContainer.ContainsKey(102)); // Absent root key returns before scanning references.
    }
}
