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
    public Task NullClientSendCallFaultsOnlyAfterWholeReplyWasEncodedAndFinalized() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var dialog = new LegacyNativeDialog(null!, scope.Packet);
            Assert.Throws<NullReferenceException>(() => dialog.AddText("encoded before call"));
            Assert.Equal(Npc(Reply.InteractTypes.Dialog, "encoded before call"), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task NullHandlesAreRetainedAndMessageBoxFinalizeDoesNotReadEitherHandle() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var dialog = new LegacyNativeDialog(null!, null!);
            Assert.Null(dialog.stream); dialog.FinalizeDialog(messagebox: true);
            Assert.Throws<NullReferenceException>(() => dialog.FinalizeDialog()); scope.Untouched(); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task LaterStreamFaultKeepsEarlierActualQueuedDialogPrefixWithoutAutomaticFinishOrRepair() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); var dialog = new LegacyNativeDialog(scope.Native.Owner, scope.Packet);
            dialog.Text("first"); dialog.stream = null!;
            Assert.Throws<NullReferenceException>(() => dialog.AddOption("later", 3));
            scope.Sent(Npc(Reply.InteractTypes.Dialog, "first"));
            Assert.Equal(Npc(Reply.InteractTypes.Dialog, "first"), NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Null(dialog.stream);
        });
}
