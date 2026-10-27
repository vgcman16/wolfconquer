using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    private sealed class LoadingTransactionWitness
    {
        private static readonly FieldInfo State = typeof(LegacyNativeNinja)
            .GetField("persistenceState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly Scope scope;
        private readonly object state;
        private readonly LegacyNativeGameClientRuntime? ninjaUser;
        private readonly LegacyNativeNinja? currentNinja;
        private readonly LegacyNativeSpellStorage? currentSpells;
        private readonly LegacyNativeEquipItemStorage? equipment;
        private readonly LegacyNativePlayerRuntime? player;
        private readonly byte badge;
        private readonly LegacyNativeSpellMutationWitness spells;
        private readonly object? rankRegistry = LegacyNativeNinjaRank.Ranks;
        private readonly RankingGraphWitness? ranks = LegacyNativeNinjaRank.Ranks is null ? null : new();
        private readonly LegacyNativePacketMutationWitness[] packets;
        private readonly byte[][] queue;
        private readonly int available;
        private readonly Dictionary<LegacyNativeNinja.Item, uint[]> items = new(ReferenceEqualityComparer.Instance);
        private readonly KeyValuePair<uint, LegacyNativeNinja.Item>[] itemEntries;
        private readonly object? itemMap;

        internal LoadingTransactionWitness(Scope scope)
        {
            this.scope = scope;
            state = State.GetValue(scope.Ninja)!;
            ninjaUser = scope.Ninja.user;
            currentNinja = scope.Client.MyNinja;
            currentSpells = scope.Client.MySpells;
            equipment = scope.Client.Equipment;
            player = scope.Client.Player;
            badge = player?.NiniaP0 ?? 0;
            spells = new(scope.Spells);
            itemMap = scope.Ninja.Items;
            itemEntries = scope.Ninja.Items?.ToArray() ?? [];
            foreach (var item in itemEntries.Select(pair => pair.Value).Where(item => item is not null))
                items.TryAdd(item, [item.ItemID, item.Level, item.Position, item.Exp, item.UK]);
            packets = Enumerable.Range(0, 12).Select(index => new LegacyNativePacketMutationWitness(scope.Used(index)))
                .Append(new(scope.Packet)).ToArray();
            queue = scope.Queue.Select(bytes => bytes.ToArray()).ToArray();
            available = scope.Available;
        }

        internal void RequireUnchanged()
        {
            Assert.Same(state, State.GetValue(scope.Ninja));
            Assert.Same(ninjaUser, scope.Ninja.user);
            Assert.Same(currentNinja, scope.Client.MyNinja);
            Assert.Same(currentSpells, scope.Client.MySpells);
            Assert.Same(equipment, scope.Client.Equipment);
            Assert.Same(player, scope.Client.Player);
            if (player is not null) Assert.Equal(badge, player.NiniaP0);
            Assert.Same(itemMap, scope.Ninja.Items);
            var currentItems = scope.Ninja.Items?.ToArray() ?? [];
            Assert.Equal(itemEntries.Select(pair => pair.Key), currentItems.Select(pair => pair.Key));
            for (var index = 0; index < itemEntries.Length; index++) Assert.Same(itemEntries[index].Value, currentItems[index].Value);
            foreach (var item in items)
                Assert.Equal(item.Value, new[] { item.Key.ItemID, item.Key.Level, item.Key.Position, item.Key.Exp, item.Key.UK });
            spells.RequireUnchanged();
            Assert.Same(rankRegistry, LegacyNativeNinjaRank.Ranks);
            ranks?.RequireUnchanged();
            Assert.Equal(queue.Length, scope.Queue.Count);
            for (var index = 0; index < queue.Length; index++) Assert.Equal(queue[index], scope.Queue.ElementAt(index));
            Assert.Equal(available, scope.Available);
            foreach (var packet in packets) packet.RequireUnchanged();
        }
    }

    private static void SeedLoadingTransaction(Scope scope)
    {
        SeedRankingRows(scope);
        scope.Item(100, 100, 9, 1, 4, 5);
        scope.Meta(1, 100, 9, 1000, 16310, 16320);
        scope.Ninja.Fire = 2;
        scope.Ninja.FireMastery = 3;
        scope.Ninja.Exp = 6;
        scope.Ninja.Flag = 7;
        scope.SetNinia(7);
        var stale = scope.Spell(16330, 8);
        stale.SoulLevel = 3;
        stale.PreviousLevel = 6;
        stale.ColdTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        stale.StampSpell = stale.ColdTime.AddMinutes(1);
        stale.Type = 12;
        stale.IsSpellWithColdTime = true;
    }

    private static void InvokeLoadingOperation(Scope scope, string operation)
    {
        switch (operation)
        {
            case "loading": scope.Ninja.Loading(); break;
            case "alternate": scope.Ninja.Alternate(); break;
            case "badge": scope.Ninja.GetLevel(); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
