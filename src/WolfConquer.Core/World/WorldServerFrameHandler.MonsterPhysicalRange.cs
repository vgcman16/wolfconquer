using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal bool IsMonsterPhysicalRangeStore(MonsterRuntimeStateStore store, WorldDataCatalog catalog) =>
        ReferenceEquals(_monsterStates, store) && ReferenceEquals(_worldData, catalog);

    // Plain read under the monster gate: never acquires a connection/actor gate
    // in reverse order. The actual Frame loop selected this existing holder.
    internal bool IsMonsterPhysicalAiTargetCurrent(MonsterRuntimeStateStore store, LegacyMonsterInstance monster,
        WorldActiveSession target, LegacyMonsterTargetSnapshot snapshot, out MonsterPhysicalAiRefusal reason)
    {
        if (!ReferenceEquals(_monsterStates, store))
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetStore, out reason);
        if (!_sessions.TryGet(target.Connection, out var current) || current is null || !ReferenceEquals(current, target.Session))
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetHolder, out reason);
        if (current.NativeGameClient?.OrdinarySpellBinding is not { IsRetired: false } binding ||
            !ReferenceEquals(binding.Store, _sessions) || !ReferenceEquals(binding.Connection, target.Connection) ||
            !ReferenceEquals(binding.Client, target.Connection.NativeGameClient))
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetBinding, out reason);
        if (!binding.PhysicalOwnerIsCurrentPlain())
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetNativeOwner, out reason);
        if (binding.Player.UID != current.Character.Uid || binding.Player.Class != current.Character.Class ||
            binding.Player.HitPoints != current.Character.MinHitPoints || binding.Player.X != current.Character.X ||
            binding.Player.Y != current.Character.Y || binding.Player.Map != current.Character.MapId ||
            binding.Player.DynamicID != monster.DynamicId)
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetScalars, out reason);
        if (!binding.Player.BitVector.TryContainFlag((int)LegacyNativeMsgUpdate.Flags.Fly, out var flying) || flying)
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetFlags, out reason);
        if (current.Character.Uid != snapshot.Uid || current.Character.MapId != monster.MapId ||
            ReadCharacterDynamicId(current.Character) != monster.DynamicId || current.Character.MinHitPoints <= 0 ||
            !snapshot.Alive || snapshot.Flying || !snapshot.SocketAlive ||
            LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag) ||
            LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, LegacyPlayerDeathState.GhostStatusFlag) ||
            LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, LegacyFlyStatusFlag) ||
            snapshot.Distance != LegacyGetDistance(current.Character.X, current.Character.Y, monster.X, monster.Y))
            return RefuseMonsterPhysicalAi(MonsterPhysicalAiRefusal.TargetSnapshot, out reason);
        reason = MonsterPhysicalAiRefusal.None;
        return true;
    }

    internal bool TryReadMonsterPhysicalRange(WorldSessionStore store, LegacyMonsterInstance instance,
        Monster family, out int range)
    {
        range = 0;
        return ReferenceEquals(_sessions, store) && _worldData is { } data &&
            _monsterStates.TryReadPhysicalRange(this, data, instance, family, out range);
    }
}

public sealed partial class WorldSessionStore
{
    internal bool TryReadMonsterPhysicalRange(LegacyMonsterInstance instance, Monster family, out int range)
    {
        range = 0;
        return framePersistenceOwner is { } frame && frame.TryReadMonsterPhysicalRange(this, instance, family, out range);
    }
}
