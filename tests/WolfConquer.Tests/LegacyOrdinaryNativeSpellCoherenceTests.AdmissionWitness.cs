using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeSpellJoinFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    private sealed class NativeAdmissionSpellWitness
    {
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly object? spells, hairface, beasts, equipment;
        private readonly uint uid, online, characterClass;
        private readonly ushort level;
        private readonly bool complete;

        internal NativeAdmissionSpellWitness(Fixture fixture)
        {
            client = fixture.Native; player = fixture.Player;
            spells = client.MySpells; hairface = client.HairfaceStorage; beasts = client.Beasts;
            equipment = client.RawEquipmentObservation;
            uid = player.UID; online = player.Online; characterClass = player.Class; level = player.Level;
            complete = player.CompleteLogin;
        }

        internal void RequireUnchanged(Fixture fixture)
        {
            Assert.Same(client, fixture.Native); Assert.Same(player, fixture.Player); Assert.Same(client, player.Owner);
            Assert.Same(spells, client.MySpells); Assert.Same(hairface, client.HairfaceStorage);
            Assert.Same(beasts, client.Beasts); Assert.Same(equipment, client.RawEquipmentObservation);
            Assert.Equal(uid, player.UID); Assert.Equal(online, player.Online);
            Assert.Equal(characterClass, player.Class); Assert.Equal(level, player.Level);
            Assert.Equal(complete, player.CompleteLogin);
        }
    }
}
