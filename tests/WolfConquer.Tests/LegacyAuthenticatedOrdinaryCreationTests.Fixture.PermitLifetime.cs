using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    internal sealed partial class CreationFixture
    {
        internal async Task<(ConquerTcpConnection Connection, string Ticket)> EnterLifetimeAccountAsync(
            uint account, string username, ConquerTcpConnection? connection = null)
        {
            var actual = connection ?? NewConnection(account);
            var ticket = await AuthenticateLifetimeAccountAsync(account, username);
            var output = Frames(account); int first = output.Count;
            await EnterAsync(ticket, actual);
            Assert.True(PacketReader.TryRead(Assert.Single(output.Skip(first)), out var reply));
            Assert.Equal(WorldSnapshotWireStatus.MissingSnapshot, WorldSnapshotResponsePacket.ReadStatus(reply));
            Assert.Equal((long)account, WorldSnapshotResponsePacket.ReadAccountId(reply));
            output.Clear();
            return (actual, ticket);
        }

        private async Task<string> AuthenticateLifetimeAccountAsync(uint account, string username)
        {
            // Owned saved-account input through the real password validator,
            // Account Frame and signed single-use handoff store. No direct
            // ticket Issue shortcut or fabricated successful redemption.
            const string password = "owned-fixture-password";
            var repository = new LegacyAccServerLogAccountRepository(new[]
            { new LegacyAccServerLoginLogEntry(username, account, "127.0.0.1", password, "00:00:00") });
            var handler = new AccountServerFrameHandler(new AccountLoginService(repository), Handoff, Log);
            var output = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
                (memory, _) => { output.Add(memory.ToArray()); return ValueTask.CompletedTask; });
            try
            {
                await handler.HandleFrameAsync(connection, ReadFrame(new InternalAccountLoginPacket(username, password).ToFrameBytes()));
                Assert.True(PacketReader.TryRead(Assert.Single(output), out var reply));
                Assert.Equal(AccountLoginWireStatus.Success, AccountLoginResponsePacket.ReadStatus(reply));
                Assert.Equal((long)account, AccountLoginResponsePacket.ReadAccountId(reply));
                Assert.True(AccountLoginResponsePacket.TryReadHandoff(reply, out var ticket, out var endpoint));
                Assert.Equal("127.0.0.1:5818", endpoint);
                return ticket;
            }
            finally { await connection.CompleteTransportCloseAsync(); }
        }

        internal Task CreateLifetimeAccountAsync(ConquerTcpConnection connection, string name) =>
            SendOnAsync(connection, new LegacyNewRolePacket(name, 1008, Selection).ToFrameBytes());

        internal void OpenLifetimeFrame()
        {
            _ = Open(); // The actual successful public factory assigns Runtime.
            Handler = NewHandler(); Connection = NewConnection(Account);
        }

        internal Dictionary<string, byte[]> LifetimeWorldFiles()
        {
            var options = Data.Options();
            var roots = options.Roots.Kinds.Select(kind => options.Roots.Root(kind)!).Append(options.MailboxRoot)
                .Select(root => Path.GetRelativePath(Data.Root, root) + Path.DirectorySeparatorChar).ToArray();
            var detached = new[] { Path.GetRelativePath(Data.Root, Data.Redeem), Path.GetRelativePath(Data.Root, Data.Claim) };
            // Authentication intentionally writes its separate signed tickets.
            // Compare every actual game/UID participant, including all history;
            // no production reader gains an ignored-root permission from this.
            return Data.Files().Where(pair => roots.Any(root => pair.Key.StartsWith(root, StringComparison.OrdinalIgnoreCase)) ||
                    detached.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }

        internal string[] LifetimeParticipantPaths(uint account = Account) => Data.Options().Roots.Kinds
            .Select(kind => Data.Options().Roots.PathFor(kind, account))
            .Append(Path.Combine(Data.Mailbox, account.ToString(CultureInfo.InvariantCulture) + ".bin")).ToArray();

        internal (uint HighWater, ulong Generation) Ledger()
        {
            var actual = JsonNode.Parse(File.ReadAllBytes(Data.State))!;
            return (actual["HighWater"]!.GetValue<uint>(), actual["Generation"]!.GetValue<ulong>());
        }

        internal void DeleteCompletedLifetimeParticipantInputs()
        {
            // Declared NEGATIVE external corruption after genuine completion,
            // not an authorized game deletion, completion proof or grant.
            var paths = LifetimeParticipantPaths(); Assert.Equal(7, paths.Length);
            string scope = Path.GetFullPath(Data.Root) + Path.DirectorySeparatorChar;
            foreach (var path in paths)
            {
                Assert.StartsWith(scope, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
                Assert.False(Directory.Exists(path));
                File.Delete(path);
            }
            foreach (var path in paths) Assert.False(File.Exists(path));
        }
    }
}
