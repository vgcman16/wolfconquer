using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private const uint ChiPurchaseUid = 1_293_600;
    private const uint ChiPurchaseNpc = 45_632;

    private static async Task RunChiPurchaseAsync(Func<ChiPurchaseFixture, Task> body,
        string donation = "115", string? flag = "0")
    {
        var fixture = new ChiPurchaseFixture();
        Exception? primary = null;
        try { await fixture.StartAsync(donation, flag); await body(fixture); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { await fixture.DisposeAsync(); }
            catch (Exception cleanup)
            {
                if (primary is null) throw;
                if (!ReferenceEquals(primary, cleanup)) throw new AggregateException(primary, cleanup);
            }
        }
    }

    // Reuse the actual reviewed admission/UID producer and existing seed/decoder
    // helpers rather than duplicating the full registry/bootstrap machinery.
    private sealed class ChiPurchaseFixture
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "wolf-world-handler-" + Guid.NewGuid().ToString("N"));
        internal readonly ConcurrentQueue<byte[]> Sent = new();
        internal readonly ChiPurchaseLog Log = new();
        private readonly ConcurrentBag<Task> calls = [];
        private OrdinaryLoginFixture? admission;
        internal WorldServerFrameHandler Handler { get; private set; } = null!;
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal WorldSessionStore Sessions => Actual<WorldSessionStore>("_sessions");
        internal LegacyTradeStateStore Trades => Actual<LegacyTradeStateStore>("_tradeStates");
        internal LegacyTradeRecoveryState Recovery => Actual<LegacyTradeRecoveryState>("_tradeRecovery");
        internal WorldOperationBarrier Operations => Actual<WorldOperationBarrier>("_worldOperations");
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "PlayersItems");
        internal string UserPath => Path.Combine(Users, ChiPurchaseUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string UidLease => Path.Combine(Items, ".managed-item-uids", "owner.lock");
        internal WorldSession Current => Sessions.TryGet(Connection, out var value) ? Assert.IsType<WorldSession>(value)
            : throw new InvalidOperationException("Actual admitted ChargeChi owner is absent.");

        internal async Task StartAsync(string donation, string? flag)
        {
            Directory.CreateDirectory(Users);
            WriteSnapshot(Users, ChiPurchaseUid, ChiPurchaseUid, donatePoints: 115,
                extraCharacterLines: flag is null ? null : "ChargeChi=" + flag);
            var text = File.ReadAllText(UserPath).Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n').Replace("\n", "\r\n", StringComparison.Ordinal);
            var donationRow = "DonatePoints=115\r\n";
            Assert.Equal(1, text.Split(donationRow, StringSplitOptions.None).Length - 1);
            text = text.Replace(donationRow, "DonatePoints=" + donation + "\r\n", StringComparison.Ordinal);
            File.WriteAllText(UserPath, text, new UTF8Encoding(true));
            admission = new(new CharacterSnapshotLoader(Users), OrdinaryLoginFixture.Catalog(
                npcs: [new Npc((int)ChiPurchaseNpc, 0, 0, 1015, 830, 582, null)]));
            Handler = admission.CreateHandler(Log);
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, _) =>
            { Sent.Enqueue(bytes.ToArray()); return ValueTask.CompletedTask; });
            await Frame(new LegacyMsgLoginGamePacket(ChiPurchaseUid).ToFrameBytes());
            admission.AssertCommitObserved();
            Assert.Contains(Sent, bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var response) &&
                response.Content.Count > 3 && response.Content[3] == "ANSWER_OK");
            Assert.Equal(ChiPurchaseUid, Current.Character.Uid);
            Assert.Null(Current.NativeGameClient);
            var first = Sent.Count;
            await Frame(LegacyNpcServerRequestPacket.Script(ChiPurchaseNpc, optionId: 1, interactType: 1).ToFrameBytes());
            Assert.Equal(ChiPurchaseNpc, Current.ActiveNpcId);
            Assert.Contains(Sent.Skip(first), bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
                reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == 2);
            Sent.Clear();
        }

        internal WorldPacketHandlerContext Context => new(Connection, Log, Current, WorldData: admission!.WorldData,
            Sessions: Sessions, PlayerItemsRoot: Items, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        { Trades = Trades, TradeOperations = Operations, TradeRecovery = Recovery };

        internal Task Purchase(WorldPacketHandlerContext? captured = null)
        {
            var bytes = new LegacyNpcDialogRequestPacket(ChiPurchaseNpc, 0, 2, 1, 0, "").ToFrameBytes();
            if (captured is null) return Frame(bytes);
            var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            return Track(registry.HandleAsync(captured, ReadFrame(bytes)).AsTask());
        }

        private Task Frame(byte[] bytes) => Track(Handler.HandleFrameAsync(Connection, ReadFrame(bytes)).AsTask());
        internal Task Track(Task task) { calls.Add(task); return task; }

        private T Actual<T>(string name)
        {
            var field = typeof(WorldServerFrameHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<T>(field.GetValue(Handler));
        }

        internal async Task DisposeAsync()
        {
            if (calls.Any(task => !task.IsCompleted))
                throw new InvalidOperationException("Owned ChargeChi root retained while a real request remains active.");
            Log.BeforeLine = null;
            var errors = new List<Exception>();
            try { if (Connection is not null) await Connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            try { admission?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
            try
            {
                var path = Path.GetFullPath(Root);
                var name = Path.GetFileName(path);
                const string prefix = "wolf-world-handler-";
                if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                        StringComparison.OrdinalIgnoreCase) || !name.StartsWith(prefix, StringComparison.Ordinal) ||
                    !Guid.TryParseExact(name[prefix.Length..], "N", out _)) throw new InvalidOperationException("Invalid owned ChargeChi cleanup target.");
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("ChargeChi cleanup failed.", errors);
        }
    }

    private sealed class ChiPurchaseLog : StringWriter
    {
        private readonly object gate = new();
        internal Action<string?>? BeforeLine;
        public override Task WriteLineAsync(string? line)
        { lock (gate) { BeforeLine?.Invoke(line); base.WriteLine(line); return Task.CompletedTask; } }
        public override string ToString() { lock (gate) return base.ToString(); }
    }
}
