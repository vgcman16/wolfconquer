using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    private static LegacyNativeHairfaceStorageTable.Hairface Hair(uint id = 321, LegacyHairFaceStorageType type = LegacyHairFaceStorageType.Hairstyle) => new()
    { ID = id, Type = type, Classes = new(), Colors = new byte[] { 1, 0, 1 }, EquippedColor = 2, Cost = 5, Name = "Owned" };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task WholeHairSetterPublishesPhysicalValueThenLoadedPacketAndRealStorageAdmission(bool loaded) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Hair(); LegacyNativeHairfaceStorageTable.Hairfaces.Add(row);
        scope.Client.FullLoading = loaded; scope.Player.Hair = 4321;
        Assert.Equal(4321u, scope.Player.Hair); Assert.Equal((byte)4, scope.Player.HairColor);
        Assert.Equal(loaded ? 1 : 0, scope.Queue.Count); Assert.Equal(loaded ? 1 : 0, scope.Client.HairfaceStorage!.Objects.Length);
        if (loaded) { Assert.Equal((ushort)2176, Opcode(scope.Queue.Peek())); Assert.Same(row, scope.Client.HairfaceStorage.Objects[0]); }
        scope.Player.HairColor = 7; Assert.Equal(7321u, scope.Player.Hair); Assert.Equal((byte)7, scope.Player.HairColor);
        if (loaded) Assert.Single(scope.Client.HairfaceStorage.Objects);
    });

    [Fact]
    public Task HairColorUsesSourceUshortWrapAndLoadedSetterKeepsPostSendCatalogFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.Hair = 65535; scope.Player.HairColor = 255;
        Assert.Equal(58927u, scope.Player.Hair); Assert.Equal((byte)58, scope.Player.HairColor);
        scope.Client.FullLoading = true; LegacyNativeHairfaceStorageTable.Hairfaces = null!;
        Assert.Throws<ArgumentNullException>(() => scope.Player.Hair = 4321);
        Assert.Equal(4321u, scope.Player.Hair); Assert.Equal((ushort)2176, Opcode(Assert.Single(scope.Queue)));
    });

    [Fact]
    public Task WholeStorageAddHasRealSendBranchSnapshotAndNullOwnerFaultAfterPublication() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var storage = scope.Client.HairfaceStorage!; var row = Hair();
        Assert.False(storage.Add(null)); Assert.True(storage.Add(row));
        Assert.Equal(Convert.FromHexString("1500EA0808012A0D080010C1021800200028002801"), Assert.Single(scope.Queue));
        var snapshot = storage.Objects; Assert.False(storage.Add(Hair())); Assert.Same(snapshot, storage.Objects);
        Assert.True(storage.Add(Hair(type:LegacyHairFaceStorageType.Avatar), false)); Assert.Equal(2, storage.Objects.Length); Assert.Single(snapshot);
        var broken = new LegacyNativeHairfaceStorageRuntime(null); var owned = Hair(123);
        Assert.Throws<NullReferenceException>(() => broken.Add(owned)); Assert.Same(owned, Assert.Single(broken.Objects));
    });

    [Theory]
    [InlineData(1008, 1, 2, true)] [InlineData(1002, 1, 2, false)] [InlineData(2007, 2, 2, true)]
    [InlineData(2008, 2, 2, false)] [InlineData(1008, 0, 1, false)]
    public Task CanEquipWholePolicyUsesExactBodyVipAndClass(int body, int sex, int vip, bool expected) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Hair(); row.Sex = (byte)sex; row.RequiredVIPLevel = 2; row.Classes = [1];
        scope.Player.Body = (ushort)body; scope.Player.VipLevel = (byte)vip;
        Assert.Equal(expected, scope.Client.HairfaceStorage!.CanEquip(row));
        row.Classes = [2]; Assert.False(scope.Client.HairfaceStorage.CanEquip(row));
        row.Sex = 0; scope.Player.VipLevel = 2; row.Classes = null;
        Assert.Throws<NullReferenceException>(() => scope.Client.HairfaceStorage.CanEquip(row));
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RealEquipAndColorMutationUseWholeHairSetterAndSendAcknowledgments(bool loaded) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Hair(); LegacyNativeHairfaceStorageTable.Hairfaces.Add(row);
        var storage = scope.Client.HairfaceStorage!; Assert.True(storage.Add(row, false)); scope.Client.FullLoading = loaded;
        Assert.True(storage.Equip(row)); Assert.Equal(2321u, scope.Player.Hair);
        Assert.Equal(loaded ? 3 : 1, scope.Queue.Count); Assert.Equal((ushort)2282, Opcode(scope.Queue.Last()));
        scope.Queue.Clear(); Assert.False(storage.EquipColor(321, 1));
        Assert.True(storage.AddColor(new LegacyNativeHairfaceStorageTable.HairColor { ID = 321, Color = 1 }));
        Assert.Equal((byte)1, row.Colors![1]); Assert.Equal((ushort)2282, Opcode(Assert.Single(scope.Queue)));
        scope.Queue.Clear(); Assert.True(storage.EquipColor(321, 1)); Assert.Equal(1321u, scope.Player.Hair);
        Assert.Equal((byte)1, row.EquippedColor); Assert.Equal(loaded ? 2 : 1, scope.Queue.Count);
        scope.Queue.Clear(); Assert.Throws<IndexOutOfRangeException>(() => storage.AddColor(new LegacyNativeHairfaceStorageTable.HairColor { ID = 321, Color = 9 }));
        Assert.Empty(scope.Queue);
    });
}
