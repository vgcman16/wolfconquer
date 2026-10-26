using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Actual saved-character Frame admission and registered NPC handlers; managed
// component/file proof, not authenticated Host birth or live client rendering.
public sealed partial class LegacyAccountManagerConquerPointsTransactionTests
{
    private const uint Uid = 2_399_653;
    private const uint NpcId = 45_632;

    private static async Task RunAsync(Func<Fixture, Task> body, string donation = "125", string conquerPoints = "400")
    {
        var fixture = new Fixture();
        Exception? primary = null;
        try { await fixture.StartAsync(donation, conquerPoints); await body(fixture); }
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

    private sealed class Fixture
    {
        internal readonly string Root = Path.Combine(Path.GetTempPath(), "wolf-account-cp-" + Guid.NewGuid().ToString("N"));
        internal readonly ConcurrentQueue<byte[]> Sent = new();
        internal readonly ConversionLog Log = new();
        internal Action<byte[]>? BeforeSend;
        private readonly ConcurrentBag<Task> calls = [];
        private LegacyItemUidRegistry? registry;
        internal WorldServerFrameHandler Handler { get; private set; } = null!;
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal WorldDataCatalog WorldData { get; private set; } = null!;
        internal WorldSessionStore Sessions => Actual<WorldSessionStore>("_sessions");
        internal LegacyTradeStateStore Trades => Actual<LegacyTradeStateStore>("_tradeStates");
        internal LegacyTradeRecoveryState Recovery => Actual<LegacyTradeRecoveryState>("_tradeRecovery");
        internal WorldOperationBarrier Operations => Actual<WorldOperationBarrier>("_worldOperations");
        internal string Users => Path.Combine(Root, "Users");
        internal string Items => Path.Combine(Root, "PlayersItems");
        internal string UserPath => Path.Combine(Users, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal string UidState => Path.Combine(Items, ".managed-item-uids", "state.json");
        internal string UidLease => Path.Combine(Items, ".managed-item-uids", "owner.lock");
        internal WorldSession Current => Sessions.TryGet(Connection, out var current) ? Assert.IsType<WorldSession>(current)
            : throw new InvalidOperationException("Actual admitted AccountManager CP owner is absent.");

        internal async Task StartAsync(string donation, string conquerPoints)
        {
            var spells = Path.Combine(Root, "PlayersSpells");
            var profs = Path.Combine(Root, "PlayersProfs");
            var quests = Path.Combine(Root, "PlayersQuests");
            var gear = Path.Combine(Root, "PlayersCombatGear");
            var mailbox = Path.Combine(Root, "PlayersMailList");
            foreach (var path in new[] { Users, Items, spells, profs, quests, gear, mailbox }) Directory.CreateDirectory(path);
            var fields = new[]
            {
                "; Owned complete Character image, retain BOM and CRLF.", "[Character]", $"UID={Uid}",
                "Name=ConquerPointsOwner", "Body=1008", "Face=154", "Haire=27", "Class=1005", "FirstClass=0",
                "SecoundeClass=0", "Reborn=2", "Level=140", "Experience=123", "Map=1002", "X=430", "Y=380",
                "PMap=1002", "PMapX=430", "PMapY=380", "Strength=10", "Agility=10", "Vitaliti=10",
                "Spirit=10", "Atributes=0", "MinHitPoints=1000", "MinMana=100", "Money=777", $"ConquerPoints={conquerPoints}",
                "BoundConquerPoints=333", "BankCps=444", "WHMoney=555", "PkPoints=0", "Online=0", "SecurityPass=0,0,0",
                "VipLevel=4", "KeepBytes=unrelated-owned-value", $"DonatePoints={donation}",
            }.ToList();
            fields.Add("ChargePromotion=2");
            fields.AddRange(["", "[Unrelated]", "Sentinel=retained", ""]);
            File.WriteAllText(UserPath, string.Join("\r\n", fields), new UTF8Encoding(true));
            new PlayerItemSnapshotLoader(Items).SaveByCharacterUid(Uid,
                new([new PlayerItemSnapshotItem { Uid = 1_000_091, ItemId = 1_000_000, StackSize = 1, Color = 5 }], [], [], [], []));
            new PlayerSpellSnapshotLoader(spells).SaveByCharacterUid(Uid, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(profs).SaveByCharacterUid(Uid, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(quests).SaveByCharacterUid(Uid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(gear).SaveByCharacterUid(Uid, CombatGearSnapshot.Empty);
            var options = new LegacyItemUidRegistryOptions(Users, Items, mailbox,
                Path.Combine(Root, "RedeemContainer.bin"), Path.Combine(Root, "ClaimContainer.bin"),
                8_000_000, "owned synthetic quiescent AccountManager CP fixture floor");
            Assert.True(LegacyItemUidRegistry.TryBootstrap(options, out registry, out var reason), reason);
            Assert.NotNull(registry);
            var loader = new CharacterSnapshotLoader(Users, Items, spells, profs, gear, quests);
            var domain = Guid.ParseExact(JsonNode.Parse(File.ReadAllBytes(UidState))!["Domain"]!.GetValue<string>(), "N");
            Assert.True(LegacyOrdinaryAdmissionUidOwner.TryCreate(registry, domain, LegacyOrdinaryAdmissionRoots.FromLoader(loader),
                mailbox, out var owner, out reason), reason);
            WorldData = LegacyItemWorldDataFixture.Create([], [], npcs: [new((int)NpcId, 0, 0, 1002, 430, 378, null)]);
            Handler = new(loader, null, WorldData, owner!, Log, ConquerProtocolProfile.VisibleDx8D13);
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, _) =>
            { var copy = bytes.ToArray(); BeforeSend?.Invoke(copy); Sent.Enqueue(copy); return ValueTask.CompletedTask; });
            await Frame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes());
            Assert.Contains(Sent, bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var response) &&
                response.Content.Count > 3 && response.Content[3] == "ANSWER_OK");
            Assert.Equal(Uid, Current.Character.Uid);
            Assert.Null(Current.NativeGameClient);
            Assert.True(owner!.IsCurrent(LegacyOrdinaryAdmissionRoots.FromLoader(loader)));
            var first = Sent.Count;
            await Frame(LegacyNpcServerRequestPacket.Script(NpcId, optionId: 1, interactType: 1).ToFrameBytes());
            Assert.Equal(NpcId, Current.ActiveNpcId);
            Assert.Contains(Sent.Skip(first), bytes => LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply) &&
                reply.InteractType == LegacyNpcReplyInteractType.Option && reply.OptionId == 3);
            Sent.Clear();
        }

        internal WorldPacketHandlerContext Context => new(Connection, Log, Current, WorldData: WorldData,
            Sessions: Sessions, PlayerItemsRoot: Items, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        { Trades = Trades, TradeOperations = Operations, TradeRecovery = Recovery };

        internal Task Purchase(WorldPacketHandlerContext? captured = null)
        {
            var bytes = new LegacyNpcDialogRequestPacket(NpcId, 0, 3, 1, 0, "").ToFrameBytes();
            if (captured is null) return Frame(bytes);
            var handlers = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
            var task = handlers.HandleAsync(captured, ReadFrame(bytes)).AsTask();
            calls.Add(task);
            return task;
        }

        private Task Frame(byte[] bytes)
        {
            var task = Handler.HandleFrameAsync(Connection, ReadFrame(bytes)).AsTask();
            calls.Add(task);
            return task;
        }

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
                throw new InvalidOperationException("Owned AccountManager CP fixture retained because a real handler is still running.");
            var errors = new List<Exception>();
            try { if (Connection is not null) await Connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            try { registry?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
            try
            {
                var path = Path.GetFullPath(Root);
                var name = Path.GetFileName(path);
                if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                        StringComparison.OrdinalIgnoreCase) || !name.StartsWith("wolf-account-cp-", StringComparison.Ordinal) ||
                    !Guid.TryParseExact(name["wolf-account-cp-".Length..], "N", out _)) throw new InvalidOperationException("Invalid owned cleanup target.");
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("AccountManager CP cleanup failed.", errors);
        }
    }

    private sealed class ConversionLog : StringWriter
    {
        private readonly object gate = new();
        public override Task WriteLineAsync(string? line)
        { lock (gate) { base.WriteLine(line); return Task.CompletedTask; } }
        public override string ToString() { lock (gate) return base.ToString(); }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
