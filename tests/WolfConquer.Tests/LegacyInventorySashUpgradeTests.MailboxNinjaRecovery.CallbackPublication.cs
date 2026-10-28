using System.Collections;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("MsgNinja", false)] [InlineData("ClassExperience", false)]
    [InlineData("MsgNinja", true)] [InlineData("ClassExperience", true)]
    public Task ColdNinjaPublicationRechecksActualStoreAfterSameValueAttributeReads(string trigger, bool retire) =>
        RunAsync(async fixture =>
        {
            fixture.SeedMailboxPackageJoin(job: 5051);
            fixture.SeedPackageJoinNinjaLine();
            var loader = new CharacterSnapshotLoader(fixture.Users, fixture.Items, null, null,
                playersCombatGearRoot: fixture.PackageGearRoot, playerQuestsRoot: null);
            var saved = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(fixture.Account));
            var client = new LegacyNativeGameClientRuntime();
            var player = client.Player;
            var sessions = new WorldSessionStore();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
            {
                fixture.Sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            }, nativeGameClient: client);
            WorldSession? admitted = null;
            InvalidOperationException? deniedReentry = null;
            var attributes = new NinjaPublicationAttributes(saved.Attributes, trigger, () =>
            {
                if (!retire) return;
                // Actual Store mutation from an input read during preparation is
                // refused. This is not an after-publication observer callback.
                deniedReentry = Assert.Throws<InvalidOperationException>(() => sessions.TryRemove(connection, out _));
                Assert.Equal("ordinarySpellPublicationRequiresCurrentCompleteOwner", deniedReentry.Message);
                ExceptionDispatchInfo.Capture(deniedReentry).Throw();
            });
            var files = fixture.Files();
            var first = fixture.Sent.Count;
            var errors = new List<Exception>();
            try
            {
                Assert.True(player.TryPrepareMailboxRecoveryLoad(client, saved, out var scalars));
                Assert.True(LegacyNativeNinja.TryPrepareMailboxRecoveryOwner(client, saved, out var prepared));
                Assert.NotNull(prepared);
                admitted = new WorldSession(fixture.Account, saved.Name, saved with { Attributes = attributes },
                    loader.LoadPlayerItemsByCharacterUid(Fixture.Uid), loader.LoadPlayerSpellsByCharacterUid(Fixture.Uid),
                    loader.LoadPlayerProficienciesByCharacterUid(Fixture.Uid),
                    CombatGear: loader.LoadCombatGearByCharacterUid(Fixture.Uid), UsersRoot: fixture.Users)
                { NativeGameClient = client };
                sessions.Set(connection, admitted);
                Assert.True(scalars!.TryPublish(sessions, connection, admitted));
                Assert.Equal(0, attributes.Callbacks);
                Assert.True(prepared.IsDetachedCurrent());
                var originalPin = client.OrdinarySpellBinding;
                var originalNinja = client.MyNinja;
                var originalSpells = client.MySpells;
                var originalProficiencies = client.MyProfs;
                var originalGear = client.CombatGear;
                var native = JsonSerializer.Serialize(new
                { player.UID, player.Class, player.ClassExperience, client.FullLoading, client.Fake });
                attributes.Armed = true;

                if (retire)
                {
                    var propagated = Assert.Throws<InvalidOperationException>(() => prepared.TryPublish(sessions, connection, admitted));
                    Assert.Same(deniedReentry, propagated);
                }
                else Assert.True(prepared.TryPublish(sessions, connection, admitted));

                Assert.Equal(1, attributes.Callbacks);
                Assert.Equal(native, JsonSerializer.Serialize(new
                { player.UID, player.Class, player.ClassExperience, client.FullLoading, client.Fake }));
                Assert.Same(client, connection.NativeGameClient);
                if (retire)
                {
                    Assert.True(sessions.TryGet(connection, out var retained));
                    Assert.Same(admitted, retained);
                    Assert.Same(client, retained!.NativeGameClient);
                    Assert.Same(player, client.Player);
                    Assert.Same(client, player.Owner);
                    Assert.Same(originalPin, client.OrdinarySpellBinding);
                    Assert.Same(originalNinja, client.MyNinja);
                    Assert.Same(originalSpells, client.MySpells);
                    Assert.Same(originalProficiencies, client.MyProfs);
                    Assert.Same(originalGear, client.CombatGear);
                    Assert.True(prepared.IsDetachedCurrent());
                    Assert.False(prepared.IsPublishedCurrent());
                }
                else
                {
                    Assert.True(sessions.TryGet(connection, out var current));
                    Assert.Same(admitted, current);
                    var ninja = Assert.IsType<LegacyNativeNinja>(client.MyNinja);
                    Assert.True(ninja.IsCombatOwnedBy(client));
                    Assert.Equal(25U, ninja.Exp);
                    Assert.Equal(2, ninja.Items.Count);
                    Assert.Equal(41U, ninja.Items[100].UK);
                    Assert.Equal(42U, ninja.Items[404].UK);
                    Assert.True(prepared.IsPublishedCurrent());
                    Assert.False(prepared.TryPublish(sessions, connection, admitted));
                    Assert.Same(ninja, client.MyNinja);
                    Assert.True(ninja.TryPrepareMailboxPoints(client, PackageJoinNinjaLine, 50, out var reward));
                    Assert.Equal(PackageJoinNinjaPointsFrame(), reward!.CopyFrame());
                    Assert.Equal(25U, ninja.Exp);
                }
                fixture.AssertFiles(files);
                Assert.Equal(first, fixture.Sent.Count);
                Assert.Null(client.Socket);
            }
            catch (Exception error) { errors.Add(error); }
            try
            {
                sessions.TryRemove(connection, out _);
                var close = connection.CompleteTransportCloseAsync();
                fixture.Calls.Add(close);
                await fixture.ObserveAsync(close);
            }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("Ninja callback publication and actual transport cleanup failed.", errors);
        });

    private sealed class NinjaPublicationAttributes : IReadOnlyDictionary<string, string>
    {
        private readonly Dictionary<string, string> values;
        private readonly string trigger;
        private readonly Action callback;
        internal bool Armed { get; set; }
        internal int Callbacks { get; private set; }
        internal NinjaPublicationAttributes(IReadOnlyDictionary<string, string> source, string trigger, Action callback)
        { values = new(source, StringComparer.OrdinalIgnoreCase); this.trigger = trigger; this.callback = callback; }
        public int Count => values.Count;
        public IEnumerable<string> Keys => values.Keys;
        public IEnumerable<string> Values => values.Values;
        public string this[string key] => values[key];
        public bool ContainsKey(string key) => values.ContainsKey(key);
        public bool TryGetValue(string key, out string value)
        {
            var found = values.TryGetValue(key, out value!);
            if (Armed && key.Equals(trigger, StringComparison.OrdinalIgnoreCase))
            { Armed = false; Callbacks++; callback(); }
            return found;
        }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
