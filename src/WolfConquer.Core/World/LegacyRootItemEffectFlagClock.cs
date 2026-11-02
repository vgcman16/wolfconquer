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
    internal static bool IsProjected(CharacterSnapshot character, int flag) =>
        character.Attributes.ContainsKey(Marker(flag));
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

    internal static CharacterSnapshot Install(WorldSessionStore? world, ConquerTcpConnection connection,
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
        var state = State(world, connection);
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
        private bool held;
        internal CharacterSnapshot Character { get; }

        private PreparedInstall(ClientClocks state, CharacterSnapshot character, int flag,
            int seconds, TimeProvider provider)
        {
            this.state = state; uid = character.Uid;
            Monitor.Enter(state.Gate, ref held);
            try
            {
                var now = provider.GetUtcNow();
                var entry = new LegacyRootItemEffectFlagClock(uid, flag,
                    LegacyArrowBladesFlagRegistration.LocalTime(provider, now), seconds);
                _ = entry.ExpiresAtLocal; // Validate the actual commit-time local deadline.
                after = state.OwnerUid == uid ? new(state.Flags) : [];
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

        // The connection gate, prepared native publication and this clock gate
        // are still held; these assignments cannot invoke a caller callback.
        internal void Publish()
        {
            if (!held) throw new InvalidOperationException("rootFlagClockPreparationRetired");
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
            if (state.OwnerUid != character.Uid)
            {
                state.Flags.Clear();
                return false;
            }
            if (!IsProjected(character, flag) ||
                !LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag) ||
                !state.Flags.TryGetValue(flag, out var current))
            {
                state.Flags.Remove(flag);
                return false;
            }
            entry = current; // Captured old object survives a later quiet replacement.
            return true;
        }
    }

    internal static void Forget(WorldSessionStore world, ConquerTcpConnection connection, long uid, int flag)
    {
        var state = Existing(world, connection);
        if (state is null) return;
        lock (state.Gate)
            if (state.OwnerUid == uid) state.Flags.Remove(flag); // Source RemoveFlag removes CURRENT registration.
    }

    internal static CharacterSnapshot ForgetProjection(CharacterSnapshot character, int flag)
    {
        if (flag is not 8 and not 9) return character;
        var attributes = LegacyCharacterAttributeMap.Copy(character.Attributes);
        return LegacyCharacterAttributeMap.Remove(attributes, Marker(flag))
            ? character with { Attributes = attributes } : character;
    }
}
