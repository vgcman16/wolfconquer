using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using GoalOwner = WolfConquer.Core.World.LegacyNativeHeroRewardsRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerLifecycleTests
{
    private sealed record SavedGoalRow(uint Key, GoalOwner.Item? Row, uint Uid, uint Unknown, byte Claim);

    private sealed class GoalAdmissionSnapshot
    {
        private readonly LegacyNativePlayerRuntime player;
        private readonly GoalOwner? goals;
        private readonly LegacyNativeDbSafeDictionary<uint, GoalOwner.Item>? book;
        private readonly object? gate, comparer;
        private readonly GoalOwner.Item[]? cache, cacheCopy;
        private readonly bool dirty;
        private readonly long revision;
        private readonly SavedGoalRow[] rows;
        private readonly ushort counter, kills;
        private readonly byte booster, killer;
        private readonly LegacyNativeMsgUpdate.Flags used;
        private readonly int packetSize, packetPosition;
        private readonly byte[] packet;
        private readonly byte[][] frames, frameCopies;

        internal unsafe GoalAdmissionSnapshot(Scope scope, LegacyNativePlayerRuntime? captured = null, GoalOwner? capturedGoals = null)
        {
            player = captured ?? scope.Player; goals = capturedGoals ?? scope.Client.HeroRewards;
            book = goals?.ArrayGoals; gate = book?.SyncRoot; comparer = book?.Comparer;
            cache = book?.MyArray; cacheCopy = cache?.ToArray(); dirty = book?.Update ?? false;
            revision = book?.Revision ?? 0;
            rows = book?.Select(pair => new SavedGoalRow(pair.Key, pair.Value, pair.Value?.UID ?? 0,
                pair.Value?.UnKnow ?? 0, pair.Value?.Claim ?? 0)).ToArray() ?? [];
            counter = player.XPCount; kills = player.KillCounter; booster = player.XPBooster;
            killer = player.XPKiller; used = player.UseXPSpell;
            packetSize = scope.Packet.Size; packetPosition = scope.Packet.Position;
            packet = new byte[scope.Packet.Capacity]; Marshal.Copy((IntPtr)scope.Packet.Memory, packet, 0, packet.Length);
            frames = scope.Queue.ToArray(); frameCopies = frames.Select(frame => frame.ToArray()).ToArray();
        }

        internal unsafe void AssertUnchanged(Scope scope, ushort? expectedCounter = null, bool requireBookBinding = true)
        {
            if (requireBookBinding && goals is not null) Assert.Same(book, goals.ArrayGoals);
            if (book is not null)
            {
                Assert.Same(gate, book.SyncRoot); Assert.Same(comparer, book.Comparer);
                Assert.Equal(cache, book.MyArray); Assert.Equal(cacheCopy, book.MyArray); Assert.Equal(dirty, book.Update); Assert.Equal(revision, book.Revision);
                var actual = book.ToArray(); Assert.Equal(rows.Length, actual.Length);
                for (int index = 0; index < rows.Length; index++)
                {
                    var saved = rows[index]; Assert.Equal(saved.Key, actual[index].Key); Assert.Same(saved.Row, actual[index].Value);
                    if (saved.Row is null) continue;
                    Assert.Equal(saved.Uid, saved.Row.UID); Assert.Equal(saved.Unknown, saved.Row.UnKnow); Assert.Equal(saved.Claim, saved.Row.Claim);
                }
            }
            Assert.Equal(expectedCounter ?? counter, player.XPCount); Assert.Equal(kills, player.KillCounter);
            Assert.Equal(booster, player.XPBooster); Assert.Equal(killer, player.XPKiller); Assert.Equal(used, player.UseXPSpell);
            Assert.Equal(packetSize, scope.Packet.Size); Assert.Equal(packetPosition, scope.Packet.Position);
            byte[] after = new byte[scope.Packet.Capacity]; Marshal.Copy((IntPtr)scope.Packet.Memory, after, 0, after.Length);
            Assert.Equal(packet, after);
            var queued = scope.Queue.ToArray(); Assert.Equal(frames.Length, queued.Length);
            for (int index = 0; index < frames.Length; index++)
            { Assert.Same(frames[index], queued[index]); Assert.Equal(frameCopies[index], queued[index]); }
        }
    }

    private static void SeedGoalPreimage(Scope scope)
    {
        scope.Player.XPCount = 19; scope.Player.KillCounter = 81;
        scope.Player.XPBooster = 67; scope.Player.XPKiller = 43;
        scope.Queue.Enqueue([0x44, 0x11, 0x22, 0x33]);
    }

    private static void CorruptGoalOwner(Scope scope, string kind)
    {
        var goals = scope.Client.HeroRewards!;
        switch (kind)
        {
            case "missing": scope.Client.HeroRewards = null; break;
            case "foreign": scope.Client.HeroRewards = new GoalOwner(new LegacyNativeGameClientRuntime()); break;
            case "book": goals.ArrayGoals = null!; break;
            case "row": goals.ArrayGoals[101] = null!; break;
            case "key": goals.ArrayGoals[101] = new() { UID = 102 }; break;
            case "zero": goals.ArrayGoals[0] = new() { UID = 0 }; break;
            case "alias":
                var row = new GoalOwner.Item { UID = 101 };
                goals.ArrayGoals[101] = row; goals.ArrayGoals[102] = row;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private sealed class GoalAdmissionComparer(LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
        GoalOwner goals, string mode, int trigger) : IEqualityComparer<uint>
    {
        internal bool Armed, Fired;
        internal int Reads;
        internal LegacyNativePlayerRuntime? ReplacementPlayer;
        internal GoalOwner? ReplacementGoals;
        internal LegacyNativeDbSafeDictionary<uint, GoalOwner.Item>? ReplacementBook;
        internal GoalOwner.Item? EarlierRow;
        internal uint TriggerKey = 101;
        internal bool HeldClient, HeldPlayer, HeldBook;
        public bool Equals(uint left, uint right) => left == right;
        public int GetHashCode(uint key)
        {
            if (!Armed || key != TriggerKey || ++Reads != trigger) return key.GetHashCode();
            Armed = false; Fired = true;
            HeldClient = Monitor.IsEntered(client.SpellAdmissionSyncRoot);
            HeldPlayer = Monitor.IsEntered(player.SpellAdmissionSyncRoot);
            HeldBook = Monitor.IsEntered(goals.ArrayGoals.SyncRoot);
            switch (mode)
            {
                case "throw": throw new InvalidOperationException("Owned goal dictionary failure.");
                case "player": client.Player = ReplacementPlayer!; break;
                case "uid": player.UID++; break;
                case "goals": client.HeroRewards = ReplacementGoals; break;
                case "retire": client.HeroRewards = null; break;
                case "book": goals.ArrayGoals = ReplacementBook!; break;
                case "earlier-row": EarlierRow!.Claim++; break;
                case "observe": break;
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
            return key.GetHashCode();
        }
    }

    private static GoalAdmissionComparer InstallGoalComparer(Scope scope, string mode, int trigger = 2)
    {
        var goals = scope.Client.HeroRewards!;
        var comparer = new GoalAdmissionComparer(scope.Client, scope.Player, goals, mode, trigger)
        {
            ReplacementPlayer = new LegacyNativePlayerRuntime(scope.Client) { UID = scope.Player.UID },
            ReplacementGoals = new GoalOwner(scope.Client), ReplacementBook = new(),
        };
        goals.ArrayGoals = new(4, comparer);
        goals.ArrayGoals.Add(101, new() { UID = 101, Claim = 7, UnKnow = 23 });
        return comparer;
    }
}
