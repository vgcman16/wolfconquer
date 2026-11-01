using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Closed calculation inputs, not a live participant or future cache owner.
    // The actual proc still proves captured metadata and every real successor.
    internal sealed class DragonhowlReceiveCalculationData
    {
        private readonly ConquerProtocolProfile actorProfile, targetProfile;
        internal FutureEquipmentReceiveData? ActorFuture { get; }
        internal FutureEquipmentReceiveData? TargetFuture { get; }

        private DragonhowlReceiveCalculationData(ConquerProtocolProfile actorProfile, ConquerProtocolProfile targetProfile,
            FutureEquipmentReceiveData? actorFuture, FutureEquipmentReceiveData? targetFuture)
        {
            if (actorProfile != ConquerProtocolProfile.VisibleDx8D13 || targetProfile != ConquerProtocolProfile.VisibleDx8D13)
                throw new InvalidOperationException("dragonhowlReceiveDataProfileUnavailable");
            this.actorProfile = actorProfile; this.targetProfile = targetProfile;
            ActorFuture = actorFuture; TargetFuture = targetFuture;
        }

        internal static DragonhowlReceiveCalculationData Create(ConquerProtocolProfile actorProfile,
            ConquerProtocolProfile targetProfile, FutureEquipmentReceiveData? actorFuture = null,
            FutureEquipmentReceiveData? targetFuture = null) => new(actorProfile, targetProfile, actorFuture, targetFuture);

        internal uint[] ReadActorFields(WorldSession source, WorldDataCatalog? catalog)
        {
            RequireFutureInputs(source, ActorFuture);
            return CalculateLegacyRuntimeStatusFields(source, catalog, actorProfile);
        }

        internal uint[] ReadTargetFields(WorldSession source, WorldDataCatalog? catalog)
        {
            RequireFutureInputs(source, TargetFuture);
            return CalculateLegacyRuntimeStatusFields(source, catalog, targetProfile);
        }

        private static void RequireFutureInputs(WorldSession source, FutureEquipmentReceiveData? future)
        {
            if (future is null) return;
            var expected = future.Session;
            // Subsequent calculated HP/status/BT/CA changes are permitted DATA
            // stages. They cannot silently substitute another level's inputs.
            // These comparisons do not inspect or grant any native authority.
            if (source.AccountId != expected.AccountId || source.Character.Uid != expected.Character.Uid ||
                source.Character.Level != expected.Character.Level || source.Character.Class != expected.Character.Class ||
                source.Character.Reborn != expected.Character.Reborn || source.Character.Strength != expected.Character.Strength ||
                source.Character.Agility != expected.Character.Agility || source.Character.Vitality != expected.Character.Vitality ||
                source.Character.Spirit != expected.Character.Spirit || source.Character.AttributePoints != expected.Character.AttributePoints ||
                source.AlternateEquipmentActive != expected.AlternateEquipmentActive ||
                !ReferenceEquals(source.PlayerItems, expected.PlayerItems) || !ReferenceEquals(source.CombatGear, expected.CombatGear) ||
                !ReferenceEquals(source.PlayerSpells, expected.PlayerSpells) ||
                !ReferenceEquals(source.EquipmentHitpointsStatus, expected.EquipmentHitpointsStatus) ||
                !UnchangedQueryOverlay(source, expected))
                throw new InvalidOperationException("dragonhowlReceiveFutureInputsChanged");
        }

        private static bool UnchangedQueryOverlay(WorldSession source, WorldSession expected)
        {
            var current = LegacyPhysicalStatusOverlay.ForOwner(source);
            var projected = LegacyPhysicalStatusOverlay.ForOwner(expected);
            return current.MaxMana == projected.MaxMana && current.JiangHuMaxMana == projected.JiangHuMaxMana &&
                current.RidingCropItemId == projected.RidingCropItemId && current.RightWeaponItemEffect == projected.RightWeaponItemEffect &&
                current.RingItemEffect == projected.RingItemEffect && current.NecklaceItemEffect == projected.NecklaceItemEffect &&
                current.PerfectionToxinEraser == projected.PerfectionToxinEraser;
        }
    }
}
