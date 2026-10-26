using System.Net;
using System.Globalization;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private const string PackageJoinNinjaLine =
        "1/25/2/3/4/5/6/7/8/9/10/11/777/12/13/14/15/16/17/2/100/2/1/31/41/404/3/9/32/42/";

    private sealed partial class Fixture
    {
        internal void SeedPackageJoinNinjaLine()
        {
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(CharacterPath));
            Assert.False(text.Contains("MsgNinja=", StringComparison.Ordinal));
            text = text.Replace("[Unrelated]", "MsgNinja=" + PackageJoinNinjaLine + Environment.NewLine + "[Unrelated]", StringComparison.Ordinal);
            File.WriteAllBytes(CharacterPath, new UTF8Encoding(false).GetBytes(text));
        }
    }

    private sealed partial class PackageJoin
    {
        private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> ComponentRegistry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        private WorldPacketHandlerContext? componentContext;
        internal LegacyNativeGameClientRuntime NativeClient => Assert.IsType<LegacyNativeGameClientRuntime>(Connection!.NativeGameClient);

        private void StartNativeComponent(LegacyMailboxPackageProducer producer)
        {
            // Explicit registered-handler component birth. This does not claim native
            // authenticated Frame/login adoption, a socket pump, or live Host wiring.
            var client = new LegacyNativeGameClientRuntime();
            var character = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.CharacterPath), fixture.CharacterPath);
            var savedGear = new CombatGearSnapshotLoader(fixture.PackageGearRoot).LoadByCharacterUid(Fixture.Uid);
            Assert.Empty(savedGear.Items);
            Assert.Empty(savedGear.BlackJadeBag.BlackJades);
            client.Player.UID = Fixture.Uid;
            typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(client.Player, checked((uint)nativeComponentJob!.Value));
            typeof(LegacyNativePlayerRuntime).GetField("_classexp", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(client.Player, uint.Parse(character.Attributes["ClassExperience"], CultureInfo.InvariantCulture));
            client.FullLoading = true; // Fixed component phase input; never toggled around publication/sending.
            var gear = new LegacyNativeMyCombatGear(client)
            { MasteryPoints = savedGear.MasteryPoints, TaoistReiki = savedGear.TaoistReiki,
                PirateExp = savedGear.PirateExp, Lottery = savedGear.Lottery };
            gear.SetClass(checked((uint)nativeComponentJob.Value));
            var air = savedGear.AirPowers;
            gear.AirPowers.SenseLevel = air.SenseLevel;
            gear.AirPowers.SenseAipPower = air.SenseAipPower;
            gear.AirPowers.ArmedLevel = air.ArmedLevel;
            gear.AirPowers.ArmedAipPower = air.ArmedAipPower;
            gear.AirPowers.OverLoadLevel = air.OverLoadLevel;
            gear.AirPowers.OverLoadAipPower = air.OverLoadAipPower;
            gear.AirPowers.Points = air.Points;
            client.CombatGear = gear;
            if (nativeComponentJob == 5051)
            {
                var ninja = new LegacyNativeNinja(client);
                client.MyNinja = ninja;
                ninja.Load(character.Attributes["MsgNinja"]);
            }
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                var bytes = frame.ToArray();
                fixture.Sent.Add(bytes);
                SendObserver?.Invoke(bytes);
                return ValueTask.CompletedTask;
            }, nativeGameClient: client);
            var sessions = new WorldSessionStore();
            sessions.BindMailboxPackages(producer);
            var session = new WorldSession(fixture.Account, "owned-native-package-component",
                character,
                new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid),
                PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty,
                CombatGear: savedGear,
                UsersRoot: fixture.Users) { NativeGameClient = client };
            sessions.Set(Connection, session);
            Assert.True(LegacySecondaryPasswordState.TryRead(session.Character.Attributes, out var pin, out var reason), reason);
            Assert.True(LegacySecondaryPasswordRuntime.TryInitialize(Connection, session, pin));
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(Connection, session));
            var mailbox = new LegacyMailboxStore(fixture.Root);
            Assert.Equal(2, mailbox.Load(Fixture.Uid).Count);
            componentContext = new(Connection, fixture.Log, session, WorldData: worldData, Sessions: sessions,
                PlayerItemsRoot: fixture.Items, Mailboxes: mailbox, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
            {
                Trades = new LegacyTradeStateStore(), TradeOperations = new WorldOperationBarrier(),
                TradeRecovery = Recovery, MailboxPackages = bindProducer ? producer : null,
            };
            Assert.Null(client.Socket); // No accepting sender/SendQueue is installed.
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(sessions, Connection, Fixture.Uid, out _));
        }
    }
}
