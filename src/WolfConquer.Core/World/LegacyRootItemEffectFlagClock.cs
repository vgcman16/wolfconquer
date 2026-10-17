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
        internal readonly Dictionary<int, LegacyRootItemEffectFlagClock> Flags = [];
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
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [Marker(flag)] = bool.TrueString,
            [DeadlineKey(flag)] = mirror,
        };
        return character with { Attributes = attributes };
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
        if (flag is not 8 and not 9 || !character.Attributes.ContainsKey(Marker(flag))) return character;
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase);
        attributes.Remove(Marker(flag));
        return character with { Attributes = attributes };
    }
}
