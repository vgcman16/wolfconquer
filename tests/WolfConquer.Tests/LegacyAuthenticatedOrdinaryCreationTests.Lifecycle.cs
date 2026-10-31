using System.Buffers;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    internal sealed partial class CreationFixture
    {
        internal async Task<string> AuthenticateAsync()
        {
            // Declared owned saved-account input, real repository/password
            // validation and real signed one-time ticket writer. No accepting repo.
            var repository = new LegacyAccServerLogAccountRepository(new[]
            { new LegacyAccServerLoginLogEntry("creation-owner", Account, "127.0.0.1", "owned-fixture-password", "00:00:00") });
            var account = new AccountServerFrameHandler(new AccountLoginService(repository), Handoff, Log);
            var frames = new List<byte[]>();
            var connection = new ConquerTcpConnection(null, new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 9959),
                (memory, _) => { frames.Add(memory.ToArray()); return ValueTask.CompletedTask; });
            try
            {
                await account.HandleFrameAsync(connection, ReadFrame(new InternalAccountLoginPacket("creation-owner", "owned-fixture-password").ToFrameBytes()));
                return ReadTicket(Assert.Single(frames));
            }
            finally { await connection.CompleteTransportCloseAsync(); }
        }

        private static string ReadTicket(byte[] bytes)
        {
            Assert.True(PacketReader.TryRead(bytes, out var reader));
            Assert.Equal(AccountLoginWireStatus.Success, AccountLoginResponsePacket.ReadStatus(reader));
            Assert.Equal((long)Account, AccountLoginResponsePacket.ReadAccountId(reader));
            Assert.True(AccountLoginResponsePacket.TryReadHandoff(reader, out var ticket, out var endpoint));
            Assert.Equal("127.0.0.1:5818", endpoint); return ticket;
        }
        internal Task EnterAsync(string? ticket = null, ConquerTcpConnection? connection = null) =>
            SendOnAsync(connection ?? Connection, new InternalWorldEnterRequestPacket(ticket ?? Ticket).ToFrameBytes());
        internal Task CreateAsync(string name = "NewOwner", ushort? body = null, ushort? selection = null) =>
            SendAsync(new LegacyNewRolePacket(name, body ?? 1008, selection ?? Selection).ToFrameBytes());
        internal Task SendAsync(byte[] bytes) => SendOnAsync(Connection, bytes);
        internal async Task SendOnAsync(ConquerTcpConnection connection, byte[] bytes)
        {
            await Handler.HandleFrameAsync(connection, ReadFrame(bytes), connections[connection].Token);
            if (observationFailure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        internal async Task ConnectRuneActorAsync()
        {
            RuneConnection = NewConnection(RuneActor);
            await SendOnAsync(RuneConnection, new LegacyMsgLoginGamePacket(RuneActor).ToFrameBytes());
            var current = CurrentFor(RuneConnection);
            Assert.Equal((long)RuneActor, current.Character.Uid); Assert.True(Store.IsBoundSpellOwnerCurrent(current.NativeGameClient!.OrdinarySpellBinding!));
            Frames(RuneActor).Clear();
        }
        internal async Task ReopenFrameAsync(Action? beforeLogin = null)
        {
            Runtime = Open(); beforeLogin?.Invoke(); Handler = NewHandler(); Connection = NewConnection(Account);
            await SendAsync(new LegacyMsgLoginGamePacket(Account).ToFrameBytes());
        }
        internal async Task CloseAsync()
        {
            BeforeSend = null; var errors = new List<Exception>();
            foreach (var pair in connections)
            {
                try { await pair.Key.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await pair.Key.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
                pair.Value.Dispose();
            }
            connections.Clear(); RuneConnection = null;
            foreach (var owner in owners.AsEnumerable().Reverse())
                try { await owner.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            owners.Clear();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
        internal static PacketFrame ReadFrame(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame;
        }
    }
}
