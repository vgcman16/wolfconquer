using WolfConquer.Core.Runtime;
using System.Net;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlOutsideRosterTests
{
    [Fact]
    public async Task SavedSameKeyPolicyCaseAliasesAreRejectedByRealAdmissionBeforeAnyProcOwnerExists()
    {
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        string text = Encoding.UTF8.GetString(File.ReadAllBytes(data.UserPath));
        Assert.Contains("\r\n[Unrelated]\r\n", text);
        // Declared invalid saved image, before actual Bootstrap. No admitted
        // session is changed and no later proc/preflight claim is made here.
        File.WriteAllBytes(data.UserPath, Encoding.UTF8.GetBytes(text.Replace("\r\n[Unrelated]\r\n",
            "\r\nUserExpRate=1\r\nuserexprate=2\r\n[Unrelated]\r\n", StringComparison.Ordinal)));
        data.Bootstrap();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(data.Loader, data.Options(), out var opened, out var reason), reason);
        await using var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        using var log = new StringWriter(); var sent = new List<byte[]>();
        var handler = new WorldServerFrameHandler(data.Loader, null, LegacyItemWorldDataFixture.Create([], []),
            LegacyRuntimeUpdateLoopOptions.Disabled, log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, _) => { sent.Add(bytes.ToArray()); return ValueTask.CompletedTask; });
        var files = data.Files();
        try
        {
            await handler.HandleFrameAsync(connection, OwnedEquipmentBehaviorFixture.ReadFrame(
                new LegacyMsgLoginGamePacket(LegacyOwnedAdmissionRuntimeFixture.Uid).ToFrameBytes()));
            Assert.False(Field<WorldSessionStore>(handler, "_sessions").TryGet(connection, out _));
            Assert.DoesNotContain(sent, bytes => LegacyMsgMessagePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) &&
                packet.Content.Count > 3 && packet.Content[3] == "ANSWER_OK");
            Assert.Contains("ordinaryAdmissionPreparation:InvalidDataException", log.ToString(), StringComparison.Ordinal);
            Assert.False(LegacyTradeJournal.HasPending(data.Users)); data.AssertFiles(files);
            Assert.Null(connection.NativeGameClient); // Managed pre-owner refusal, not a fabricated native proc test.
        }
        finally { await connection.CompleteTransportCloseAsync(); }
    }
}
