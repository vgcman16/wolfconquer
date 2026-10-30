using System.Reflection;
using WolfConquer.Core.World;
using Reply = WolfConquer.Core.World.LegacyNativeNpcReply;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDialogPacketTests
{
    [Fact]
    public Task CompleteDialogMethodChainUsesOneRealStreamAndPreservesEveryNativeAliasAndQueueOrder() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var dialog = new LegacyNativeDialog(scope.Native.Owner, scope.Packet);
            Assert.Same(scope.Packet, dialog.stream);
            Assert.Same(scope.Native.Owner, typeof(LegacyNativeDialog).GetField("client", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dialog));
            Assert.Same(dialog, dialog.CreateMessageBox("Box")); dialog.FinalizeDialog(messagebox: true);
            Assert.Same(dialog, dialog.AddText("Wolf")); Assert.Same(dialog, dialog.Text("Alias"));
            Assert.Same(dialog, dialog.Option("Opt", 7)); Assert.Same(dialog, dialog.Avatar(8));
            Assert.Same(dialog, dialog.AddAvatar(9)); Assert.Same(dialog, dialog.AddPage("https://example.invalid/"));
            Assert.Same(dialog, dialog.AddOption("Default")); Assert.Same(dialog, dialog.AddOption("Explicit", 10));
            Assert.Same(dialog, dialog.AddInput("Typed", 11)); Assert.Same(dialog, dialog.AddInput("Max", 12, byte.MaxValue));
            dialog.FinalizeDialog();
            scope.Sent(Npc(Reply.InteractTypes.MessageBox, "Box", ushort.MaxValue), Npc(Reply.InteractTypes.Dialog, "Wolf"),
                Npc(Reply.InteractTypes.Dialog, "Alias"), Npc(Reply.InteractTypes.Option, "Opt", option: 7),
                Npc(Reply.InteractTypes.Avatar, "", 8), Npc(Reply.InteractTypes.Avatar, "", 9),
                Npc(Reply.InteractTypes.Page, "https://example.invalid/", option: byte.MaxValue),
                Npc(Reply.InteractTypes.Option, "Default", option: byte.MaxValue), Npc(Reply.InteractTypes.Option, "Explicit", option: 10),
                Npc(Reply.InteractTypes.Input, "Typed", 16, 11), Npc(Reply.InteractTypes.Input, "Max", byte.MaxValue, 12),
                Npc(Reply.InteractTypes.Finish, "", display: false));
            Assert.Equal(Npc(Reply.InteractTypes.Finish, "", display: false), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.NotSame(scope.Native.Queue.ElementAt(0), scope.Native.Queue.ElementAt(1));
        });

    [Fact]
    public Task DialogUsesCurrentPublicStreamWithoutChangingItsOriginalClientBindingOrOldQueuedBytes() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var dialog = new LegacyNativeDialog(scope.Native.Owner, scope.Packet);
            dialog.AddText("first"); dialog.stream = scope.Second; dialog.AddText("second");
            scope.Sent(Npc(Reply.InteractTypes.Dialog, "first"), Npc(Reply.InteractTypes.Dialog, "second"));
            Assert.Equal(Npc(Reply.InteractTypes.Dialog, "first"), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(Npc(Reply.InteractTypes.Dialog, "second"), NativeSpellTestFixture.Bytes(scope.Second));
            scope.Second.Seek(4); scope.Second.Write(123u); Assert.Equal(Npc(Reply.InteractTypes.Dialog, "second"), scope.Native.Queue.Last());
        });

    [Fact]
    public Task FakeRecipientDoesNotPreventRealDialogEncodingAndNoSocketIsConsulted() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Native.Owner.Fake = true; scope.Native.Owner.Socket = null;
            var dialog = new LegacyNativeDialog(scope.Native.Owner, scope.Packet);
            Assert.Same(dialog, dialog.AddText("fake"));
            Assert.Equal(Npc(Reply.InteractTypes.Dialog, "fake"), NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task MissingDialogClientRefusesBeforeEncodingOrSendingAnyReply() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            byte[] before = scope.Raw(0, scope.Packet.Capacity);
            Assert.Throws<ArgumentNullException>(() => new LegacyNativeDialog(null!, scope.Packet));
            scope.Untouched(); Assert.Empty(scope.Native.Queue);
            Assert.Equal(before, scope.Raw(0, scope.Packet.Capacity));
        });

    [Fact]
    public Task DialogRequiresBothInitialHandlesAndMessageBoxFinalizationRemainsANoOp() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            byte[] before = scope.Raw(0, scope.Packet.Capacity);
            Assert.Throws<ArgumentNullException>(() => new LegacyNativeDialog(scope.Native.Owner, null!));
            Assert.Throws<ArgumentNullException>(() => new LegacyNativeDialog(null!, null!));
            var dialog = new LegacyNativeDialog(scope.Native.Owner, scope.Packet);
            dialog.stream = null!; dialog.FinalizeDialog(messagebox: true);
            Assert.Throws<ArgumentNullException>(() => dialog.FinalizeDialog());
            scope.Untouched(); Assert.Empty(scope.Native.Queue);
            Assert.Equal(before, scope.Raw(0, scope.Packet.Capacity));
        });

    [Fact]
    public Task LaterStreamFaultKeepsEarlierActualQueuedDialogPrefixWithoutAutomaticFinishOrRepair() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var dialog = new LegacyNativeDialog(scope.Native.Owner, scope.Packet);
            dialog.Text("first"); dialog.stream = null!;
            Assert.Throws<ArgumentNullException>(() => dialog.AddOption("later", 3));
            scope.Sent(Npc(Reply.InteractTypes.Dialog, "first"));
            Assert.Equal(Npc(Reply.InteractTypes.Dialog, "first"), NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Null(dialog.stream);
        });
}
