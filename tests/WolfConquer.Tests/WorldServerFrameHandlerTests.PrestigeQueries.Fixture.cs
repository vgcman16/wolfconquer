using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static async Task RunPrestigeQueryAsync(Func<PrestigeQueryFixture, Task> body,
        IReadOnlyList<PrestigeRankEntry>? rows = null)
    {
        var fixture = new PrestigeQueryFixture();
        Exception? primary = null;
        try { await fixture.StartAsync(rows); await body(fixture); }
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

    // This uses the ordinary saved-login admission/UID producer and real session
    // store. The send callback only records frames; it cannot accept admission,
    // save state, execute SQL, substitute a native runtime or publish a session.
    private sealed class PrestigeQueryFixture : IAsyncDisposable
    {
        private const uint Uid = 1_293_600;
        private readonly string root = Path.Combine(Path.GetTempPath(),
            "wolf-world-handler-" + Guid.NewGuid().ToString("N"));
        private readonly List<ConquerTcpConnection> ownedConnections = [];
        private OrdinaryLoginFixture? admission;
        internal readonly List<byte[]> Sent = [];
        internal readonly StringWriter Log = new(CultureInfo.InvariantCulture);
        internal List<PrestigeRankEntry> SourceRows { get; private set; } = [];
        internal WorldServerFrameHandler Handler { get; private set; } = null!;
        internal ConquerTcpConnection Connection { get; private set; } = null!;
        internal WorldSessionStore Sessions => GetSessions(Handler);
        internal WorldSession Current => GetSession(Handler, Connection);
        internal WorldDataCatalog WorldData => admission!.WorldData;

        internal async Task StartAsync(IReadOnlyList<PrestigeRankEntry>? rows)
        {
            var users = Path.Combine(root, "Users");
            Directory.CreateDirectory(users);
            WriteSnapshot(users, Uid, Uid);
            SourceRows = rows?.ToList() ?? [PrestigeRankingQueryTestData.Row()];
            var ordinary = OrdinaryLoginFixture.Catalog();
            var catalog = LegacyItemWorldDataFixture.Create(ordinary.Items, [],
                classLevelStats: ordinary.ClassLevelStats,
                magicTypeOperations: ordinary.MagicTypeOperations,
                prestigeRanks: SourceRows);
            admission = new(new CharacterSnapshotLoader(users), catalog);
            Handler = admission.CreateHandler(Log);
            Connection = CreateOwnedSendingConnection(Sent);
            await Handler.HandleFrameAsync(Connection,
                ReadFrame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()));
            admission.AssertCommitObserved();
            Assert.Equal(Uid, Current.Character.Uid);
            Assert.Null(Current.NativeGameClient);
            Assert.Contains(Sent, bytes => LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var response) &&
                response.Content.Count > 3 && response.Content[3] == "ANSWER_OK");
            Sent.Clear();
        }

        internal WorldPacketHandlerContext Context => new(Connection, Log, Current,
            WorldData: WorldData, Sessions: Sessions, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        internal ConquerTcpConnection CreateOwnedSendingConnection(List<byte[]> frames)
        {
            var connection = CreateSendingConnection(frames);
            ownedConnections.Add(connection);
            return connection;
        }

        internal Task Query(byte[]? request = null, WorldPacketHandlerContext? context = null,
            CancellationToken cancellationToken = default)
        {
            var frame = ReadFrame(request ?? ChampionRequest());
            return context is null
                ? Handler.HandleFrameAsync(Connection, frame, cancellationToken).AsTask()
                : PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)])
                    .HandleAsync(context, frame, cancellationToken).AsTask();
        }

        internal Dictionary<string, byte[]> SavedFiles() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Equals("owner.lock", StringComparison.Ordinal))
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

        internal void AssertFilesUnchanged(IReadOnlyDictionary<string, byte[]> expected)
        {
            var actual = SavedFiles();
            Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
            foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
        }

        public async ValueTask DisposeAsync()
        {
            var errors = new List<Exception>();
            var ownersClosed = await CloseOwnedTransportsAndAdmissionAsync(errors);
            try { Log.Dispose(); } catch (Exception error) { errors.Add(error); }
            try
            {
                var path = ValidatedOwnedRoot();
                if (!ownersClosed)
                    throw new InvalidOperationException($"Prestige-query fixture retained at '{path}'; " +
                        "transport or admission-owner closure was not confirmed.");
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Prestige-query cleanup failed.", errors);
        }

        private async ValueTask<bool> CloseOwnedTransportsAndAdmissionAsync(List<Exception> errors)
        {
            var confirmed = true;
            foreach (var connection in ownedConnections)
            {
                try { await connection.CompleteTransportCloseAsync(); }
                catch (Exception error) { confirmed = false; errors.Add(error); }
            }
            try { admission?.Dispose(); }
            catch (Exception error) { confirmed = false; errors.Add(error); }
            return confirmed;
        }

        private string ValidatedOwnedRoot()
        {
            var path = Path.GetFullPath(root);
            var name = Path.GetFileName(path);
            const string prefix = "wolf-world-handler-";
            if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                    StringComparison.OrdinalIgnoreCase) || !name.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[prefix.Length..], "N", out _))
                throw new InvalidOperationException("Invalid owned prestige-query cleanup target.");
            return path;
        }
    }

    private static byte[] ChampionRequest(uint entityUid = 0) => new LegacyRankMemberShowPacket(0,
        [LegacyRankMemberShowEntry.Empty with { Type = 1, EntityUid = entityUid }]).ToFrameBytes();
}
