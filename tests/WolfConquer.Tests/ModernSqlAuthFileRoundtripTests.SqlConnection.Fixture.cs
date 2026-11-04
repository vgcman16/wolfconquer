using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private static Task RunSqlConnectionAsync(Func<SqlConnectionFixture, NativePlayerLoopback, Task> body,
        bool attachFileLoader = true, ConquerProtocolProfile profile = ConquerProtocolProfile.VisibleDx8D13) =>
        RunSqlHandoffAsync(async sql =>
        {
            var f = new SqlConnectionFixture(sql, attachFileLoader, profile);
            var errors = new List<Exception>();
            try
            {
                await NativePlayerLoopback.RunAsync(ConquerTcpWireMode.LegacyGameServer,
                    network => body(f, network), f.Handler.HandleFrameAsync,
                    f.Handler.ReadCurrentSessionForNativeOwnerObservation);
            }
            catch (Exception error) { errors.Add(error); }
            await f.CleanupAsync(errors);
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        });

    private sealed class SqlConnectionFixture
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-sql-connection-" + Guid.NewGuid().ToString("N"));
        private readonly NativePlayerDiagnosticLog log = new();
        private readonly List<ConquerTcpConnection> connections = [];
        private readonly List<SqlWorldBundleCustody> unexpectedOwners = [];
        private readonly Dictionary<string, byte[]> files;
        internal readonly InMemoryAccountHandoffTicketStore LegacyTickets = new();
        internal SqlHandoffFixture Sql { get; }
        internal AccountHandoffService LegacyHandoff { get; }
        internal WorldServerFrameHandler Handler { get; }

        internal SqlConnectionFixture(SqlHandoffFixture sql, bool attachFileLoader, ConquerProtocolProfile profile)
        {
            Sql = sql;
            string users = Path.Combine(root, "Users"); Directory.CreateDirectory(users);
            File.WriteAllBytes(Path.Combine(users, SqlWorldStateTestData.FirstCharacter + ".ini"),
                Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(SqlWorldStateTestData.Character(SqlWorldStateTestData.FirstCharacter))
                    .Replace("Online=1\r\n", "Online=7\r\n", StringComparison.Ordinal)));
            File.WriteAllBytes(Path.Combine(users, SqlWorldStateTestData.SecondCharacter + ".ini"),
                SqlWorldStateTestData.Character(SqlWorldStateTestData.SecondCharacter));
            File.WriteAllText(Path.Combine(root, "owned-sentinel.txt"), "SQL retention must not publish file admission.");
            LegacyHandoff = new(LegacyTickets);
            Handler = new WorldServerFrameHandler(attachFileLoader ? new CharacterSnapshotLoader(users) : null,
                LegacyHandoff, ArenaStatisticsProvider.Empty, TeamArenaStatisticsProvider.Empty,
                worldData: null, LegacyRuntimeUpdateLoopOptions.Disabled, log, protocolProfile: profile);
            files = CaptureFiles();
        }

        internal async Task<SqlConnectionPeer> ConnectAsync(NativePlayerLoopback network, bool establishFrameLifetime = true)
        {
            var wire = await network.ConnectGameAsync(false, captureOutput: true);
            var birth = await network.NextAsync();
            Assert.Equal(NativePlayerLoopback.ProbeOpcode, birth.Opcode);
            Assert.Null(birth.Before); Assert.Null(birth.After);
            Assert.True(birth.Connection.HasRegisteredRelicWire);
            Assert.True(birth.Connection.HasSendRoute); Assert.True(birth.Connection.HasDisconnectRoute);
            var before = CaptureSqlConnectionNative(birth.Connection);
            Assert.Equal(0U, before.Values.Uid); Assert.False(before.FullLoading); Assert.False(before.Fake);
            var peer = new SqlConnectionPeer(wire, birth.Connection, connections.Count,
                before);
            connections.Add(birth.Connection);
            if (establishFrameLifetime)
            {
                // ProbeOpcode bypasses the configured Frame. A genuine
                // non-probe dispatch establishes its existing EOF callback.
                await wire.SendAsync(new PacketWriter(65001, 0).ToArray(), network.Token);
                var dispatched = await network.NextAsync(); Assert.Equal((ushort)65001, dispatched.Opcode);
                Assert.Same(peer.Connection, dispatched.Connection); Assert.Null(dispatched.Before); Assert.Null(dispatched.After);
                AssertSqlConnectionNativeUnchanged(before, peer.Connection); AssertFilesUnchanged();
            }
            return peer;
        }

        internal ValueTask<SqlConnectionHandoffResult> RetainAsync(SqlConnectionPeer peer,
            SqlAccountWorldHandoffOffer offer, ConquerEndpoint? endpoint = null, CancellationToken token = default) =>
            Handler.RetainSqlWorldHandoffAsync(peer.Connection, Sql.Database.CreateWorldStateRepository(),
                offer.Token, endpoint ?? SqlHandoffFixture.Endpoint, token);

        internal async Task AssertExcludedAsync(long accountId)
        {
            var attempted = await Sql.Database.CreateWorldStateRepository().TryAcquireBundleCustodyAsync(accountId);
            // Register an unexpected actual owner before asserting, so a broken
            // positive cannot leak a real session when this test fails.
            if (attempted.Custody is { } owner) unexpectedOwners.Add(owner);
            Assert.False(attempted.IsAcquired); Assert.Null(attempted.Custody);
            Assert.Equal(SqlWorldStateFailure.SessionCustodyConflict, attempted.Failure);
        }

        internal async Task CloseAsync(NativePlayerLoopback network, SqlConnectionPeer peer, bool emptyOutput = true)
        {
            await network.CloseClientTransportAsync(peer.Index);
            Assert.True(peer.Connection.HasObservedTransportClose);
            AssertSqlConnectionNativeUnchanged(peer.Before, peer.Connection);
            Assert.Null(Handler.ReadCurrentSessionForNativeOwnerObservation(peer.Connection));
            AssertFilesUnchanged();
            if (emptyOutput) Assert.Empty(peer.Wire.CopyCapturedServerFramesAtEof());
        }

        internal void AssertFilesUnchanged()
        {
            var actual = CaptureFiles(); Assert.Equal(files.Keys.Order(), actual.Keys.Order());
            foreach (var pair in files) Assert.Equal(pair.Value, actual[pair.Key]);
        }

        private Dictionary<string, byte[]> CaptureFiles() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);

        internal async Task CleanupAsync(List<Exception> errors)
        {
            foreach (var connection in connections)
                try { await Handler.ReleaseRetainedSqlWorldHandoffAsync(connection); }
                catch (Exception error) { errors.Add(error); }
            foreach (var owner in unexpectedOwners)
                try { await owner.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            try
            {
                log.AssertNoWorldRuntimeDiagnostics();
                Assert.DoesNotContain("PARITY-BUG", log.Text, StringComparison.Ordinal);
            }
            catch (Exception error) { errors.Add(error); }
            try { log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) return; // Retain the owned fixture for diagnosis.
            string absolute = Path.GetFullPath(root);
            string temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(Path.GetDirectoryName(absolute), temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(absolute).StartsWith("wolf-sql-connection-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe owned SQL connection fixture cleanup.");
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }
    }

    private sealed record SqlConnectionPeer(NativePlayerWireClient Wire, ConquerTcpConnection Connection,
        int Index, SqlConnectionNativeImage Before);
}
