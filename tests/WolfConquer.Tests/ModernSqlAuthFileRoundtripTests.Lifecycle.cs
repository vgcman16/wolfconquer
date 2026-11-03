using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ModernSqlAuthFileRoundtripTests
{
    private sealed partial class SqlWorldFixture
    {
        internal async Task OpenWorldAsync()
        {
            Assert.Null(Runtime); observerFailure = null;
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            Handler = new(Data.Loader, handoff, catalog, LegacyRuntimeUpdateLoopOptions.Disabled, log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            await AdmitAsync(FirstUid, OwnedModernSqlAuthDatabase.FirstUsername, first: true);
            await AdmitAsync(SecondUid, OwnedModernSqlAuthDatabase.SecondUsername, first: false);
            AssertConverged(); Sent.Clear(); OtherSent.Clear();
        }

        private async Task AdmitAsync(uint expectedUid, string username, bool first)
        {
            var prior = tickets.Tickets.Select(ticket => ticket.Token).ToHashSet(StringComparer.Ordinal);
            var forward = await AuthenticateAsync(username, OwnedModernSqlAuthDatabase.SyntheticPassword);
            Assert.Equal(LegacyAccountForwardType.Ready, forward.Type);
            Assert.Equal(expectedUid, forward.Identifier); Assert.NotEqual(expectedUid + 1000, forward.Identifier);
            var issued = Assert.Single(tickets.Tickets.Where(ticket => !prior.Contains(ticket.Token)));
            Assert.Equal(expectedUid, issued.AccountId); Assert.Equal(username, issued.Username);
            // The existing legacy wire forwards an identifier. It does not
            // redeem the separately issued ticket; this test adds no bypass.
            var native = new LegacyNativeGameClientRuntime();
            var peer = new RootLoopbackFramePeer(bytes =>
            {
                if (first)
                {
                    try { BeforeSend?.Invoke(bytes); } catch (Exception error) { observerFailure ??= error; throw; }
                    Sent.Add(bytes);
                }
                else OtherSent.Add(bytes);
                return ValueTask.CompletedTask;
            }, native);
            InstallTradeWireObserver(peer, expectedUid);
            if (first) Peer = peer; else OtherPeer = peer;
            await Handler.HandleFrameAsync(peer.Connection, ReadFrame(new LegacyMsgLoginGamePacket(forward.Identifier).ToFrameBytes()));
            await peer.DrainObservations();
            var current = CurrentFor(peer);
            Assert.Equal(expectedUid, current.AccountId); Assert.Equal(expectedUid, current.Character.Uid);
            Assert.Same(native, current.NativeGameClient); Assert.NotNull(native.OrdinarySpellBinding);
            Assert.Same(current.PlayerItems, native.OrdinarySpellBinding.Items.Image.FinalSnapshot);
            Assert.True(native.OrdinarySpellBinding.Items.Image.IsCurrentPlain());
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(peer.Connection, current));
        }

        internal async Task CloseWorldAsync()
        {
            BeforeSend = null; BeforeTradeWire = null; var errors = new List<Exception>();
            foreach (var peer in new[] { Peer, OtherPeer }) if (peer is not null)
            {
                try { await peer.Connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await peer.Connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
                try { await peer.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            }
            Peer = OtherPeer = null;
            if (Runtime is { } active) try { await active.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            Runtime = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
}
