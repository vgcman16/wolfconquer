using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using HairProto = WolfConquer.Core.World.LegacyNativeMsgHairfaceStorage.MsgHairfaceStorageProto;
using Operation = WolfConquer.Core.World.LegacyNativeMsgHairfaceStorage.Operation;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCharacterReadTests
{
    private static void HairRequest(Scope scope, HairProto proto)
    {
        scope.Packet.CreateHairface(proto); scope.Packet.Seek(4);
        var handler = typeof(LegacyNativeMsgHairfaceStorage).GetMethod("Process", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.True(handler.IsPrivate);
        try { handler.Invoke(null, new object[] { scope.Client, scope.Packet }); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    private static HairProto HairResponse(byte[] bytes)
    {
        Assert.Equal((ushort)2282, Opcode(bytes)); using var packet = new LegacyNativePacket(bytes);
        packet.Seek(4); packet.GetHairface(out var proto); return proto;
    }

    [Fact]
    public Task WholeLoginReceiverUsesActualStoredColorsAndRetainsCatalogIndexEquippedBug() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var hair = Hair(); var avatar = Hair(50, LegacyHairFaceStorageType.Avatar);
        scope.Client.HairfaceStorage!.Add(hair, false); scope.Client.HairfaceStorage.Add(avatar, false);
        scope.Player.Hair = 2321; scope.Player.Face = 50;
        LegacyNativeHairfaceStorageTable.Hairfaces.AddRange(new[] { avatar, hair });
        HairRequest(scope, new HairProto { Type = LegacyHairFaceStorageAction.Login });
        var first = HairResponse(Assert.Single(scope.Queue)); Assert.Equal(2, first.Items!.Length);
        Assert.Equal(321u, first.Items[0].ID); Assert.False(first.Items[0].Equipped); Assert.False(first.Items[1].Equipped);
        Assert.Equal(new long[] { 2, 5 }, first.Items[0].Colors); Assert.Null(first.Items[1].Colors);
        scope.Queue.Clear(); LegacyNativeHairfaceStorageTable.Hairfaces.Clear(); LegacyNativeHairfaceStorageTable.Hairfaces.AddRange(new[] { hair, avatar });
        HairRequest(scope, new HairProto { Type = LegacyHairFaceStorageAction.Login });
        Assert.All(HairResponse(Assert.Single(scope.Queue)).Items!, item => Assert.True(item.Equipped));
    });

    [Fact]
    public Task LoginShortCatalogRetainsArrayFaultBeforeSendingInsteadOfRepairingIndexLookup() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var hair = Hair(); var avatar = Hair(50, LegacyHairFaceStorageType.Avatar);
        scope.Client.HairfaceStorage!.Add(hair, false); scope.Client.HairfaceStorage.Add(avatar, false);
        scope.Player.Hair = 2321; scope.Player.Face = 50; LegacyNativeHairfaceStorageTable.Hairfaces.Add(hair);
        Assert.Throws<IndexOutOfRangeException>(() => HairRequest(scope, new HairProto { Type = LegacyHairFaceStorageAction.Login }));
        Assert.Empty(scope.Queue); Assert.Equal(2, scope.Client.HairfaceStorage.Objects.Length);
    });

    [Fact]
    public Task BuyReceiverChargesBeforeActualAdmissionAndDoesNotRefundDuplicateStorageAdd() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var hair = Hair(); LegacyNativeHairfaceStorageTable.Hairfaces.Add(hair);
        scope.Player.Money = 100; scope.Queue.Clear();
        var buy = new HairProto { Type = LegacyHairFaceStorageAction.Buy, Hair = new Operation { ID = 0, dwParam = 321 } };
        HairRequest(scope, buy); Assert.Equal(95, scope.Player.Money);
        Assert.Equal(new ushort[] { 2176, 2282 }, scope.Queue.Select(Opcode)); Assert.Same(hair, Assert.Single(scope.Client.HairfaceStorage!.Objects));
        scope.Queue.Clear(); HairRequest(scope, buy); Assert.Equal(90, scope.Player.Money);
        Assert.Equal((ushort)2176, Opcode(Assert.Single(scope.Queue))); Assert.Single(scope.Client.HairfaceStorage.Objects);
        scope.Queue.Clear(); HairRequest(scope, new HairProto { Type = LegacyHairFaceStorageAction.Equip, Hair = new Operation { ID = 0, dwParam = 321 } });
        Assert.Equal(2321u, scope.Player.Hair); Assert.Equal(LegacyHairFaceStorageAction.Equip, HairResponse(Assert.Single(scope.Queue)).Type);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task BuyColorChargesBeforeSourceExistsCheckAndOnlyAdmittedStorageGetsColorReply(bool owns) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var hair = Hair(); if (owns) scope.Client.HairfaceStorage!.Add(hair, false);
        LegacyNativeHairfaceStorageTable.HairColors.Add(new() { ID = 321, Color = 1, Cost = 3 });
        scope.Player.Money = 10; scope.Queue.Clear();
        HairRequest(scope, new HairProto { Type = LegacyHairFaceStorageAction.BuyColor, HairColor = new Operation { ID = 321, dwParam = 1 } });
        Assert.Equal(7, scope.Player.Money); Assert.Equal(owns ? 2 : 1, scope.Queue.Count);
        if (owns) { Assert.Equal((byte)1, hair.Colors![1]); Assert.Equal(LegacyHairFaceStorageAction.AddColor, HairResponse(scope.Queue.Last()).Type); }
        else Assert.Empty(scope.Client.HairfaceStorage!.Objects);
    });

    [Theory]
    [InlineData((int)LegacyHairFaceStorageAction.Avatars)] [InlineData((int)LegacyHairFaceStorageAction.HairStyles)]
    public Task TabReceiverKeepsSourceSinglePlaceholderAndZeroColor(int action) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); HairRequest(scope, new HairProto { Type = (LegacyHairFaceStorageAction)action });
        var reply = HairResponse(Assert.Single(scope.Queue)); Assert.Equal((LegacyHairFaceStorageAction)action, reply.Type);
        var placeholder = Assert.Single(reply.AvatarsOrHairTap!); Assert.Equal(0u, placeholder.ID); Assert.Equal(new long[] { 0 }, placeholder.Colors);
        Assert.Null(reply.Items);
    });

    [Theory]
    [InlineData((int)LegacyHairFaceStorageAction.Add)] [InlineData((int)LegacyHairFaceStorageAction.AddColor)]
    public Task DefinedNoopActionsRemainNoopInsideWholeReceiver(int action) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); HairRequest(scope, new HairProto { Type = (LegacyHairFaceStorageAction)action });
        Assert.Empty(scope.Queue); Assert.Empty(scope.Client.HairfaceStorage!.Objects);
    });

    [Fact]
    public Task MissingBuyOperationKeepsNativeNullFaultAndDoesNotCharge() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); LegacyNativeHairfaceStorageTable.Hairfaces.Add(Hair()); scope.Player.Money = 10; scope.Queue.Clear();
        Assert.Throws<NullReferenceException>(() => HairRequest(scope, new HairProto { Type = LegacyHairFaceStorageAction.Buy }));
        Assert.Equal(10, scope.Player.Money); Assert.Empty(scope.Queue);
    });
}
