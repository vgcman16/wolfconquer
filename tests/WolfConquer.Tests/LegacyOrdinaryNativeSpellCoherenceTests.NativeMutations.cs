using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task GenuineNativeAddRemoveRebornAndClearUpdateTheCurrentSession()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); f.RequireCommitObserved();
            await NativePacketTestThread.Run(() =>
            {
                var storage = RequireCoherent(f);
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                storage.Add(packet, 6002, 6, 2, 3, 444);
                var added = Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 6002);
                Assert.Equal(6, added.Level); Assert.Equal(444, added.Experience);
                Assert.Equal(2, added.SoulLevel); Assert.Equal(3, added.PreviousLevel);
                RequireCoherent(f);
                storage.Remove(6002, packet);
                Assert.DoesNotContain(f.Current.PlayerSpells.Spells, s => s.Id == 6002); RequireCoherent(f);
                Assert.True(storage.RebornSpell(packet, 6001));
                var reborn = Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == 6001);
                Assert.Equal(0, reborn.Level); Assert.Equal(4, reborn.PreviousLevel); Assert.Equal(0, reborn.Experience);
                RequireCoherent(f);
                storage.ClearSpells([6001], packet);
                Assert.Single(f.Current.PlayerSpells.Spells); Assert.Equal(6001, f.Current.PlayerSpells.Spells[0].Id);
                RequireCoherent(f);
            });
        });
    }

    [Fact]
    public async Task CompleteLoginRetainsNativeSpellOwnerRowsAndCanonicalMembership()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f);
            var map = storage.ClientSpells; var rows = map.ToArray();
            Assert.True(LegacyCompleteLoginRuntime.TryCapture(f.Connection, current, out var owner));
            Assert.True(f.Store.TryCompleteLegacyLogin(f.Connection, current, owner, out var updated));
            Assert.Same(f.Current, updated); Assert.True(f.Player.CompleteLogin);
            Assert.Same(current.PlayerSpells, f.Current.PlayerSpells);
            Assert.Same(storage, f.Native.MySpells); Assert.Same(map, storage.ClientSpells);
            Assert.Equal(rows, map.ToArray()); RequireCoherent(f);
        });
    }

    [Fact]
    public async Task RemovingOrdinaryHolderRetiresNativeAuthorityEvenWhenTheSameRecordIsReinserted()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f);
            Assert.True(f.Store.TryRemove(f.Connection, out var removed)); Assert.Same(current, removed);
            Assert.True(Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding).IsRetired);
            Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection, current));
            await NativePacketTestThread.Run(() =>
            {
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                var old = storage.ClientSpells; var rows = old.ToArray();
                Assert.Throws<InvalidOperationException>(() => storage.Add(packet, 6002, 1));
                Assert.Same(old, storage.ClientSpells); Assert.Equal(rows, old.ToArray());
                Assert.Null(f.CurrentOrNull);
            });
        });
    }
}
