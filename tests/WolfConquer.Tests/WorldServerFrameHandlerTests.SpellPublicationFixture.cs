using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private sealed class SavedSpellPublicationFixture : IDisposable
    {
        internal readonly StringWriter Log = new();
        internal readonly List<byte[]> Sent = [];
        internal readonly WorldServerFrameHandler Handler;
        internal readonly ConquerTcpConnection Connection;
        private readonly OrdinaryLoginFixture admission;
        private readonly string root;
        internal Action<byte[]>? ObserveSend;
        internal string SpellPath => Path.Combine(root, "PlayersSpells", "1293600.bin");
        internal WorldSession Current => GetSession(Handler, Connection);
        internal WorldSessionStore Store => GetSessions(Handler);

        internal SavedSpellPublicationFixture(string root, bool alreadyCyclone)
        {
            this.root = root;
            var users = Path.Combine(root, "Users");
            Directory.CreateDirectory(users);
            WriteSnapshot(users, 1293600, uid: 1293600, classCode: 1000, level: 110);
            Directory.CreateDirectory(Path.GetDirectoryName(SpellPath)!);
            PlayerSpellSnapshotEntry[] spells = alreadyCyclone ? [Learned, LearnedCyclone] : [Learned];
            var bytes = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(spells);
            bytes[15] = 0xA7; // Opaque first-record padding must survive ordinary admission.
            File.WriteAllBytes(SpellPath, bytes);
            var config = new ClientConfigRecord(2, "OwnedSpellFixture", 1002, "430", 430, "380", 380,
                "0001", 1, "0000", 0, 1, 3, "127.0.0.1", 9958, "owned-client-config", 1,
                "2 OwnedSpellFixture 1002 430 380 0001 0000 1 3 127.0.0.1 9958", []);
            var data = LegacyItemWorldDataFixture.Create([], [],
                classLevelStats: OrdinaryLoginFixture.Catalog().ClassLevelStats, clientConfigs: [config]);
            admission = new OrdinaryLoginFixture(new CharacterSnapshotLoader(users), data);
            Handler = admission.CreateHandler(Log);
            Connection = CreateSendingConnection(Sent, (frame, _) =>
            {
                ObserveSend?.Invoke(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        }

        internal Task Login() => Handler.HandleFrameAsync(Connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes())).AsTask();
        internal Task Confirm(byte[] request) => Handler.HandleFrameAsync(Connection, ReadFrame(request)).AsTask();
        internal Task AssertAdmission() => admission.AssertCommittedAndRepeatedAsync(Handler, Connection, Sent,
            expectedRuntimeHitpoints: 29_836);
        internal void EndLifetime()
        {
            var field = typeof(WorldServerFrameHandler).GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var actual = Assert.IsType<LegacyTradeStateStore>(field.GetValue(Handler));
            lock (actual.SyncRoot) actual.MarkConnectionEnded(Connection);
        }
        internal Dictionary<string, byte[]> Files() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".ini" or ".bin")
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);
        internal void AssertFiles(Dictionary<string, byte[]> before)
        {
            var after = Files();
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
        }
        public void Dispose() { admission.Dispose(); Log.Dispose(); }
    }

    private static readonly PlayerSpellSnapshotEntry Learned = new(6001, 4, 1111, 2, 3, 1);
    private static readonly PlayerSpellSnapshotEntry LearnedCyclone = new(1110, 6, 2222, 5, 4, 2);
    private static readonly byte[] NewCycloneWire =
    [
        0x19, 0x00, 0x35, 0x08, 0, 0, 0, 0, 0x56, 0x04,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];
    private static readonly byte[] LearnedWire =
    [
        0x19, 0x00, 0x35, 0x08, 0x57, 0x04, 0, 0, 0x71, 0x17,
        0, 0, 4, 0, 0, 0, 9, 0, 0, 0, 0, 0x12, 0x01, 0, 0,
    ];
    private static readonly byte[] LearnedCycloneWire =
    [
        0x19, 0x00, 0x35, 0x08, 0xAE, 0x08, 0, 0, 0x56, 0x04,
        0, 0, 6, 0, 0, 0, 12, 0, 0, 0, 0, 0x12, 0x01, 0x01, 0,
    ];
    private static byte[] ConfirmSpellRequest() => new MsgActionPacket
    {
        ObjId = 1_293_600, Timestamp = 1234, Type = MsgActionType.ConfirmSpells,
        PositionX = 830, PositionY = 584, DwParam = 0x12345678, Facing = 3,
    }.ToFrameBytes();
    private static bool IsSpellFrame(byte[] bytes) => ReadFrame(bytes).Header.Opcode == 2101;
}
