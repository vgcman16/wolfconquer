using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Real mapped Stigma/MagicShield producers install native local clocks; attributes are only a mirror.
// Character attributes are a compatibility projection, NEVER the authority for this owner.
internal sealed record LegacyRootItemEffectFlagClock(
    long OwnerUid, int Flag, DateTime Timer, int Seconds)
{
    private WorldSession? publishedOwner;
    private object? publishedIdentity;
    private LegacyNativeAttributeMapProof? publishedMap;

    private sealed class ClientClocks
    {
        internal readonly object Gate = new();
        internal long OwnerUid;
        internal Dictionary<int, LegacyRootItemEffectFlagClock> Flags = [];
    }
    private sealed class WorldClocks
    {
        internal readonly ConditionalWeakTable<ConquerTcpConnection, ClientClocks> Clients = new();
    }
    private static readonly ConditionalWeakTable<WorldSessionStore, WorldClocks> Worlds = new();
    private static readonly ConditionalWeakTable<ConquerTcpConnection, ClientClocks> WithoutStore = new();
    internal DateTime ExpiresAtLocal => Timer.AddSeconds(Seconds);
    private static string Marker(int flag) => "LegacyRootItemEffectFlag." + flag + ".NativeOwned";
    // The producer writes owned keys through case-insensitive unique-key
    // resolution while retaining the source comparer. Read them by that same
    // rule so a single preserved spelling is not mistaken for absence.
    internal static bool IsProjected(CharacterSnapshot character, int flag) =>
        TryReadOwnedKey(character, Marker(flag), out _);
    private static bool TryReadOwnedKey(CharacterSnapshot character, string key, out string value)
    {
        var fields = LegacyCharacterAttributeMap.Copy(character.Attributes);
        return LegacyCharacterAttributeMap.TryRead(fields, key, out value);
    }
    private static ClientClocks State(WorldSessionStore? world, ConquerTcpConnection connection) =>
        world is null ? WithoutStore.GetValue(connection, static _ => new()) :
            Worlds.GetValue(world, static _ => new()).Clients.GetValue(connection, static _ => new());
    private static ClientClocks? Existing(WorldSessionStore world, ConquerTcpConnection connection) =>
        Worlds.TryGetValue(world, out var clocks) && clocks.Clients.TryGetValue(connection, out var state)
            ? state : null;
    private static string DeadlineKey(int flag) => flag switch
    {
        9 => WorldPacketHandlers.RuntimeLegacyStigmaExpiresAtUtcAttribute,
        8 => WorldPacketHandlers.RuntimeLegacyMagicShieldExpiresAtUtcAttribute,
        _ => throw new ArgumentOutOfRangeException(nameof(flag)),
    };

    // Check both actual clock domains before any XP/cost admission. Zero is
    // a representable source duration and remains a valid immediate expiry.
    internal static bool CanRepresentDuration(uint seconds, TimeProvider provider)
    {
        if (seconds > int.MaxValue) return false;
        try
        {
            var now = provider.GetUtcNow();
            _ = now.AddSeconds(seconds);
            _ = LegacyArrowBladesFlagRegistration.LocalTime(provider, now).AddSeconds(seconds);
            return true;
        }
        catch (Exception error) when (error is ArgumentOutOfRangeException or OverflowException) { return false; }
    }

    internal static CharacterSnapshot InstallDetached(ConquerTcpConnection connection,
        CharacterSnapshot character, int flag, int seconds, TimeProvider provider)
    {
        if (flag is not 8 and not 9)
            throw new ArgumentOutOfRangeException(nameof(flag));
        if (!LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag))
            throw new ArgumentException("A real Stigma/MagicShield flag insertion must precede registration.", nameof(character));
        var now = provider.GetUtcNow();
        var mirror = now.AddSeconds(seconds).ToString("O", CultureInfo.InvariantCulture);
        var entry = new LegacyRootItemEffectFlagClock(character.Uid, flag,
            LegacyArrowBladesFlagRegistration.LocalTime(provider, now), seconds);
        var state = State(null, connection);
        lock (state.Gate)
        {
            if (state.OwnerUid != character.Uid) state.Flags.Clear();
            state.OwnerUid = character.Uid;
            state.Flags[flag] = entry; // Called only after a fresh native AddFlag or quiet AddSpellFlag replacement.
        }
        var attributes = LegacyCharacterAttributeMap.Copy(character.Attributes);
        LegacyCharacterAttributeMap.Set(attributes, Marker(flag), bool.TrueString);
        LegacyCharacterAttributeMap.Set(attributes, DeadlineKey(flag), mirror);
        return character with { Attributes = attributes };
    }

    // A prepared clock holds the real clock owner's gate while the Store
    // commits the matching holder. No registration changes before commit.
    internal sealed class PreparedInstall : IDisposable
    {
        private readonly ClientClocks state;
        private readonly Dictionary<int, LegacyRootItemEffectFlagClock> after;
        private readonly long uid;
        private readonly LegacyRootItemEffectFlagClock added;
        private readonly int[] priorKeys;
        private readonly long beforeOwnerUid;
        private readonly KeyValuePair<int, LegacyRootItemEffectFlagClock>[] beforeEntries;
        private WorldSession? preparedOwner;
        private object? preparedIdentity;
        private LegacyNativeAttributeMapProof? preparedMap;
        private bool held;
        internal CharacterSnapshot Character { get; }

        private PreparedInstall(ClientClocks state, CharacterSnapshot character, int flag,
            int seconds, TimeProvider provider)
        {
            this.state = state; uid = character.Uid;
            Monitor.Enter(state.Gate, ref held);
            try
            {
                beforeOwnerUid = state.OwnerUid;
                beforeEntries = state.Flags.ToArray();
                var now = provider.GetUtcNow();
                var entry = new LegacyRootItemEffectFlagClock(uid, flag,
                    LegacyArrowBladesFlagRegistration.LocalTime(provider, now), seconds);
                added = entry;
                _ = entry.ExpiresAtLocal; // Validate the actual commit-time local deadline.
                after = state.OwnerUid == uid ? new(state.Flags) : [];
                priorKeys = after.Keys.ToArray();
                after[flag] = entry;
                var fields = LegacyCharacterAttributeMap.Copy(character.Attributes);
                LegacyCharacterAttributeMap.Set(fields, Marker(flag), bool.TrueString);
                LegacyCharacterAttributeMap.Set(fields, DeadlineKey(flag), now.AddSeconds(seconds).ToString("O", CultureInfo.InvariantCulture));
                Character = character with { Attributes = fields };
            }
            catch { Dispose(); throw; }
        }

        internal static PreparedInstall Create(WorldSessionStore world, ConquerTcpConnection connection,
            CharacterSnapshot character, int flag, int seconds, TimeProvider provider)
        {
            if (flag is not 8 and not 9 || !LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag))
                throw new ArgumentException("An actual inserted item effect flag is required.");
            return new(State(world, connection), character, flag, seconds, provider);
        }

        internal void BindPrepared(WorldSession final, object identity)
        {
            if (!held || final.Character.Uid != uid || !IsProjected(final.Character, added.Flag))
                throw new InvalidOperationException("rootFlagClockOwnerUnavailable");
            preparedOwner = final; preparedIdentity = identity;
            preparedMap = LegacyNativeAttributeMapProof.CaptureRaw(final.Character.Attributes);
            if (!preparedMap.IsCurrentPlain()) throw new InvalidOperationException("rootFlagClockOwnerChanged");
        }

        // Preallocated witness used by the actual prepared Store publisher
        // before its writer and again before native/holder publication.
        internal bool IsCurrentPlain()
        {
            if (!held || preparedOwner is null || preparedIdentity is null || preparedMap is null ||
                !preparedMap.IsCurrentPlain() || state.OwnerUid != beforeOwnerUid || state.Flags.Count != beforeEntries.Length)
                return false;
            foreach (var entry in beforeEntries)
                if (!state.Flags.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry.Value)) return false;
            return true;
        }

        // The connection gate, prepared native publication and this clock gate
        // are still held; these assignments cannot invoke a caller callback.
        internal void Publish(WorldSessionStore.RootClockPublication permit)
        {
            if (!WorldSessionStore.ConsumeRootClockPermit(permit, this) || !held || preparedOwner is null || preparedIdentity is null || preparedMap is null || !preparedMap.IsCurrentPlain())
                throw new InvalidOperationException("rootFlagClockPreparationRetired");
            added.publishedOwner = preparedOwner; added.publishedIdentity = preparedIdentity; added.publishedMap = preparedMap;
            foreach (var key in priorKeys)
                if (key != added.Flag && (!state.Flags.TryGetValue(key, out var current) ||
                    !after.TryGetValue(key, out var previous) || !ReferenceEquals(current, previous))) after.Remove(key);
            state.OwnerUid = uid;
            state.Flags = after;
        }

        public void Dispose() { if (held) { held = false; Monitor.Exit(state.Gate); } }
    }

    // Read-only observation of the real clock owner, including orphaned rows.
    // This never creates, repairs or grants a clock registration.
    internal static bool HasRegistration(WorldSessionStore world, ConquerTcpConnection connection, long uid, int flag)
    {
        var state = Existing(world, connection);
        if (state is null) return false;
        lock (state.Gate) return state.OwnerUid == uid && state.Flags.ContainsKey(flag);
    }

    internal static bool TryRead(WorldSessionStore world, ConquerTcpConnection connection,
        CharacterSnapshot character, int flag, out LegacyRootItemEffectFlagClock entry)
    {
        entry = null!;
        var state = Existing(world, connection);
        if (state is null) return false;
        lock (state.Gate)
        {
            if (state.OwnerUid != character.Uid || !IsProjected(character, flag) ||
                !LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag) ||
                !state.Flags.TryGetValue(flag, out var current)) return false;
            entry = current; // Captured old object survives a later quiet replacement.
            return true;
        }
    }

    // Inert data from the actual per-connection clock gate. A null entry means
    // an explicitly observed absent registration, never a newly minted timer.
    internal sealed record ExpiryObservation(int Flag, LegacyRootItemEffectFlagClock? Entry,
        bool Projected, bool Visible);

    internal static ExpiryObservation? CaptureExpiry(WorldSessionStore world, ConquerTcpConnection connection,
        WorldSession session, object identity, int flag, DateTimeOffset now, TimeProvider provider)
    {
        if (flag is not 8 and not 9 || !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate) return null;
        var character = session.Character;
        var state = Existing(world, connection);
        if (state is null)
        {
            bool projected = IsProjected(character, flag);
            bool visible = LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag);
            if (projected) return visible ? null : new(flag, null, true, false);
            if (!TryReadOwnedKey(character, DeadlineKey(flag), out var absentRaw) ||
                !DateTimeOffset.TryParse(absentRaw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var absentDeadline) ||
                absentDeadline > now.ToUniversalTime()) return null;
            return new(flag, null, false, visible);
        }
        lock (state.Gate)
        {
            var entry = state.OwnerUid == character.Uid && state.Flags.TryGetValue(flag, out var value) ? value : null;
            bool projected = IsProjected(character, flag);
            bool visible = LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag);
            if (projected)
            {
                if (!visible) return new(flag, entry, true, false);
                return entry is not null && ReferenceEquals(entry.publishedOwner, session) &&
                    ReferenceEquals(entry.publishedIdentity, identity) && entry.publishedMap?.IsCurrentPlain() == true && entry.ExpiresAtLocal <= LegacyArrowBladesFlagRegistration.LocalTime(provider, now)
                    ? new(flag, entry, true, true) : null;
            }
            // An orphaned or newly installed native clock cannot be retired
            // merely because an older projection has a matching UID/deadline.
            if (entry is not null || !TryReadOwnedKey(character, DeadlineKey(flag), out var raw) ||
                !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var deadline) || deadline > now.ToUniversalTime()) return null;
            return new(flag, null, false, visible);
        }
    }

    internal sealed class PreparedExpiry : IDisposable
    {
        private readonly ClientClocks? state;
        private readonly Dictionary<int, LegacyRootItemEffectFlagClock>? after;
        private readonly KeyValuePair<int, LegacyRootItemEffectFlagClock>[] retainedBefore;
        private readonly long ownerUid;
        private bool held;
        private PreparedExpiry(ClientClocks? state, long uid, Dictionary<int, LegacyRootItemEffectFlagClock>? after,
            KeyValuePair<int, LegacyRootItemEffectFlagClock>[] retainedBefore)
        { this.state = state; ownerUid = uid; this.after = after; this.retainedBefore = retainedBefore; held = state is not null; }
        internal static PreparedExpiry? TryCreate(WorldSessionStore world, ConquerTcpConnection connection,
            WorldSession session, object identity, ExpiryObservation observation)
        {
            if (observation.Flag is not 8 and not 9 || !WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate) return null;
            var current = session.Character;
            var state = Existing(world, connection);
            if (state is null)
            {
                try
                {
                    if (observation.Entry is not null || IsProjected(current, observation.Flag) != observation.Projected ||
                        LegacyPlayerDeathState.HasStatusFlag(current.Attributes, observation.Flag) != observation.Visible) return null;
                    // The real Store gate prevents an actual Store-backed install
                    // from racing this observed absence through the commit.
                    return new(null, current.Uid, null, []);
                }
                catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                { return null; }
            }
            Monitor.Enter(state.Gate);
            try
            {
                var actual = state.OwnerUid == current.Uid && state.Flags.TryGetValue(observation.Flag, out var entry) ? entry : null;
                if (!ReferenceEquals(actual, observation.Entry) || actual is not null &&
                    (!ReferenceEquals(actual.publishedOwner, session) || !ReferenceEquals(actual.publishedIdentity, identity) || actual.publishedMap?.IsCurrentPlain() != true) ||
                    IsProjected(current, observation.Flag) != observation.Projected ||
                    LegacyPlayerDeathState.HasStatusFlag(current.Attributes, observation.Flag) != observation.Visible)
                { Monitor.Exit(state.Gate); return null; }
                var after = new Dictionary<int, LegacyRootItemEffectFlagClock>(state.Flags);
                if (actual is not null) after.Remove(observation.Flag);
                // Allocate all retained key/reference witnesses before the
                // actual Store writer and its other publication participants.
                var retainedBefore = after.ToArray();
                return new(state, current.Uid, after, retainedBefore);
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
            { Monitor.Exit(state.Gate); return null; }
            catch { Monitor.Exit(state.Gate); throw; }
        }
        internal void Publish(WorldSessionStore.RootClockPublication permit)
        {
            if (!WorldSessionStore.ConsumeRootClockPermit(permit, this)) throw new InvalidOperationException("rootFlagExpiryRetired");
            if (state is null) return;
            if (!held || after is null) throw new InvalidOperationException("rootFlagExpiryRetired");
            if (state.OwnerUid != ownerUid) return;
            // The real holder publisher can retire a different clock before
            // this commit. Never resurrect or adopt a changed generation.
            foreach (var retained in retainedBefore)
                if (!state.Flags.TryGetValue(retained.Key, out var current) || !ReferenceEquals(current, retained.Value))
                    after.Remove(retained.Key);
            state.Flags = after;
        }
        public void Dispose() { if (held) { held = false; Monitor.Exit(state!.Gate); } }
    }

    // Prepared by the actual Store publisher while its connection gate is held.
    // The clock gate stays held until publication or disposal. Equal DATA alone
    // cannot start a lineage: the old entry must name the exact expected holder.
    internal sealed class PreparedSuccession : IDisposable
    {
        private readonly ClientClocks state;
        private readonly (LegacyRootItemEffectFlagClock Entry, LegacyNativeAttributeMapProof Before,
            LegacyNativeAttributeMapProof? After)[] transfers;
        private readonly WorldSession expected, final;
        private readonly object identity;
        private bool held;
        private PreparedSuccession(ClientClocks state, WorldSession expected, WorldSession final, object identity,
            (LegacyRootItemEffectFlagClock, LegacyNativeAttributeMapProof, LegacyNativeAttributeMapProof?)[] transfers)
        { this.state = state; this.expected = expected; this.final = final; this.identity = identity; this.transfers = transfers; held = true; }
        internal static PreparedSuccession? Prepare(WorldSessionStore world, ConquerTcpConnection connection,
            WorldSession? expected, WorldSession final, object identity)
        {
            var state = Existing(world, connection);
            if (state is null || expected is null) return null;
            Monitor.Enter(state.Gate);
            try
            {
                var transfers = new List<(LegacyRootItemEffectFlagClock, LegacyNativeAttributeMapProof, LegacyNativeAttributeMapProof?)>();
                foreach (var entry in state.Flags.Values)
                {
                    if (!ReferenceEquals(entry.publishedOwner, expected) || !ReferenceEquals(entry.publishedIdentity, identity) ||
                        entry.publishedMap is not { } before || !before.IsCurrentPlain()) continue;
                    LegacyNativeAttributeMapProof? after = null;
                    bool same;
                    try { same = SameProjection(expected.Character, final.Character, entry.Flag); }
                    catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
                    {
                        // Invalid operated DATA is not a legitimate effect removal.
                        // Retiring its clock while keeping a visible flag would
                        // turn a rejected projection into an unbounded effect.
                        throw new InvalidDataException("rootFlagSuccessionProjectionInvalid", error);
                    }
                    if (same)
                    {
                        after = LegacyNativeAttributeMapProof.CaptureRaw(final.Character.Attributes);
                        if (!after.IsCurrentPlain()) throw new InvalidOperationException("rootFlagSuccessionChanged");
                    }
                    transfers.Add((entry, before, after));
                }
                if (transfers.Count == 0) { Monitor.Exit(state.Gate); return null; }
                return new(state, expected, final, identity, transfers.ToArray());
            }
            catch { Monitor.Exit(state.Gate); throw; }
        }
        private static bool SameProjection(CharacterSnapshot before, CharacterSnapshot after, int flag)
        {
            _ = LegacyWindWalkerStatusProjection.Read(before);
            _ = LegacyWindWalkerStatusProjection.Read(after);
            if (before.Uid != after.Uid || !IsProjected(before, flag) || !IsProjected(after, flag) ||
                !LegacyPlayerDeathState.HasStatusFlag(before.Attributes, flag) || !LegacyPlayerDeathState.HasStatusFlag(after.Attributes, flag)) return false;
            var a = LegacyCharacterAttributeMap.Copy(before.Attributes);
            var b = LegacyCharacterAttributeMap.Copy(after.Attributes);
            foreach (var key in new[] { Marker(flag), DeadlineKey(flag), flag == 8 ?
                WorldPacketHandlers.RuntimeLegacyMagicShieldRemoveOnDeadAttribute : WorldPacketHandlers.RuntimeLegacyStigmaRemoveOnDeadAttribute,
                LegacyStatusFlagDeathRegistration.Key(flag) })
            {
                bool x = LegacyCharacterAttributeMap.TryRead(a, key, out var av);
                bool y = LegacyCharacterAttributeMap.TryRead(b, key, out var bv);
                if (x != y || x && av != bv) return false;
            }
            return true;
        }
        internal bool IsCurrentPlain()
        {
            if (!held) return false;
            foreach (var (entry, before, after) in transfers)
                if (!state.Flags.TryGetValue(entry.Flag, out var current) || !ReferenceEquals(current, entry) ||
                    !ReferenceEquals(entry.publishedOwner, expected) || !ReferenceEquals(entry.publishedIdentity, identity) ||
                    !ReferenceEquals(entry.publishedMap, before) || !before.IsCurrentPlain() || after is not null && !after.IsCurrentPlain()) return false;
            return true;
        }
        internal void Publish(WorldSessionStore.RootClockPublication permit)
        {
            if (!WorldSessionStore.ConsumeRootClockPermit(permit, this) || !held) throw new InvalidOperationException("rootFlagSuccessionRetired");
            foreach (var (entry, _, after) in transfers)
            {
                if (after is null) state.Flags.Remove(entry.Flag);
                else { entry.publishedOwner = final; entry.publishedIdentity = identity; entry.publishedMap = after; }
            }
        }
        public void Dispose() { if (held) { held = false; Monitor.Exit(state.Gate); } }
    }

    internal static CharacterSnapshot ForgetProjection(CharacterSnapshot character, int flag)
    {
        if (flag is not 8 and not 9) return character;
        var attributes = LegacyCharacterAttributeMap.Copy(character.Attributes);
        return LegacyCharacterAttributeMap.Remove(attributes, Marker(flag))
            ? character with { Attributes = attributes } : character;
    }
}
