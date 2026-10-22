using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed partial class LegacyEquipmentQueryExecution
    {
        private bool ResetNativeMagicQuery()
        {
            if (!Refresh()) return false;
            return LegacyNativeMagicQueryClient.Reset(Context.Sessions, Context.Connection,
                Request.Scope.OwnerUid, Request.Scope.OwnerNativeClientIdentity);
        }

        private bool NativeMagic(Func<LegacyNativeMagicQueryFields, LegacyNativeMagicQueryFields> update) =>
            Refresh() && LegacyNativeMagicQueryClient.Update(
                Context.Sessions, Context.Connection, Request.Scope.OwnerUid, update,
                Request.Scope.OwnerNativeClientIdentity);

        private LegacyNativeMagicQueryFields NativeMagicCurrent =>
            LegacyNativeMagicQueryClient.Read(Context.Sessions, Context.Connection,
                Request.Scope.OwnerUid, Request.Scope.OwnerNativeClientIdentity);

        private bool AddNativeMagic(LegacyNativeMagicQueryFields add) => NativeMagic(current => current with
        {
            MagicAttack = unchecked(current.MagicAttack + add.MagicAttack),
            MagicPercent = unchecked(current.MagicPercent + add.MagicPercent),
            SkillCStrike = unchecked(current.SkillCStrike + add.SkillCStrike),
            MagicDamageIncrease = unchecked(current.MagicDamageIncrease + add.MagicDamageIncrease),
        });

        private bool NativeMagicItemBeforeCircle(PlayerItemSnapshotItem item) =>
            NativeMagic(current =>
            {
                // Source Equip796/809 capture each CURRENT durable slot before AddGem/Circle awaits.
                current = (item.Position % 50) switch
                {
                    4 => current with { RightWeaponItemId = item.ItemId },
                    5 => current with { LeftWeaponItemId = item.ItemId },
                    _ => current,
                };
                return LegacyPlayerStatusCalculator.NativeMagicItemGems(current, item);
            });

        private bool NativeMagicItemAfterCircle(PlayerItemSnapshotItem item) =>
            Refresh() && AddNativeMagic(LegacyPlayerStatusCalculator.NativeMagicItemAfterCircle(
                item, Context.WorldData));

        private bool NativeMagicBeforeFury()
        {
            if (!Refresh()) return false;
            if (Context.WorldData is { } data &&
                TryGetLegacyEligibleEquippedRuneLevel(Session, data, "witchery", out var witchery))
            {
                if (!NativeMagic(current =>
                {
                    if (current.MagicAttack >= 15_000) return current;
                    var add = witchery switch
                    { 1 => 2_000U, 2 => 2_300U, 3 => 2_600U, 4 => 3_000U, 5 => 3_500U,
                        6 => 4_000U, 7 => 4_500U, 8 => 5_000U, 9 => 6_000U, _ => 0U };
                    return current with { MagicAttack = Math.Min(15_000U, unchecked(current.MagicAttack + add)) };
                })) return false;
            }
            if (Context.WorldData is { } chantData &&
                TryGetLegacyEligibleEquippedRuneLevel(Session, chantData, "Nature`sChant", out var chant))
            {
                var percent = chant switch
                { 1 => 13U, 2 => 14U, 3 => 15U, 4 => 16U, 5 => 17U, 6 => 18U, 7 => 19U,
                    8 => 22U, 9 => 24U, _ => 0U };
                if (!NativeMagic(current => current with
                    { MagicAttack = unchecked(current.MagicAttack + current.MagicAttack / 100 * percent) }))
                    return false;
            }
            return true;
        }

        private bool NativeMagicBeforeGear()
        {
            if (!Refresh()) return false;
            var wardrobe = EnumerateLegacyWardrobeItems(Session.PlayerItems).ToArray();
            var mount = wardrobe.Where(item => ItemType.GetLegacyEquipmentPosition(item.ItemId) == 17)
                .Select(item => item.Uid).Distinct().Count() > 100;
            var garment = wardrobe.Where(item => ItemType.GetLegacyEquipmentPosition(item.ItemId) == 9)
                .Select(item => item.Uid).Distinct().Count() > 100;
            if (!NativeMagic(current => current with
                { MagicAttack = unchecked(current.MagicAttack + (mount ? 1_000U : 0U) +
                        (garment ? 600U : 0U) + 1),
                    MagicDamageIncrease = unchecked(current.MagicDamageIncrease + current.ThunderGemValue) }))
                return false;
            if (!AddNativeMagic(LegacyPlayerStatusCalculator.NativeMagicBeforeGear(
                    Session, Context.WorldData, LegacyNativePerfectionClient.Prestige(
                        Context.Sessions, Context.Connection, Request.Scope.OwnerUid,
                        Request.Scope.OwnerNativeClientIdentity)))) return false;
            return AddNativeMagic(NativeMagicInnerPower(Session, Context.WorldData));
        }

        private bool NativeMagicLateProfiles()
        {
            if (!Refresh() || !AddNativeMagic(NativeMagicSubClass(Session, Context.WorldData))) return false;
            if (!HasLegacyPlayerMagicInterserverAttribute(Session) &&
                !LegacyNativeMagicQueryClient.RebuildJiangHu(Context.Sessions, Context.Connection,
                    Request.Scope.OwnerUid, owner => NativeMagicJiangHu(owner, Context.WorldData),
                    Request.Scope.OwnerNativeClientIdentity)) return false;
            return AddNativeMagic(LegacyNativeMagicQueryClient.ReadJiangHu(
                Context.Sessions, Context.Connection, Request.Scope.OwnerUid,
                Request.Scope.OwnerNativeClientIdentity));
        }

        private bool NativeMagicGear() => Refresh() &&
            AddNativeMagic(LegacyPlayerStatusCalculator.NativeMagicGear(Session, Context.WorldData));
        private bool NativeMagicMythsoul() => Refresh() &&
            AddNativeMagic(LegacyPlayerStatusCalculator.NativeMagicMythsoul(Session, Context.WorldData));
        private bool NativeMagicRuneStorage() => Refresh() &&
            AddNativeMagic(LegacyPlayerStatusCalculator.NativeMagicRuneStorage(Session, Context.WorldData));
    }

    private static LegacyPlayerStatusPacket CreateNativeMagicCachedPlayerStatusResponse(
        WorldPacketHandlerContext context, WorldSession target,
        ConquerTcpConnection? targetConnection = null, object? expectedIdentity = null)
    {
        var fields = CalculateLegacyRuntimeStatusFields(target, context.WorldData);
        var cached = default(LegacyNativeMagicQueryFields);
        if (targetConnection is not null)
            cached = LegacyNativeMagicQueryClient.Read(
                context.Sessions, targetConnection, target.Character.Uid, expectedIdentity);
        else if (TryGetSessionByUid(context, CheckedUInt32(target.Character.Uid, nameof(target.Character.Uid)),
                out var current))
            cached = LegacyNativeMagicQueryClient.Read(context.Sessions, current.Connection, target.Character.Uid);
        fields[LegacyPlayerStatusPacket.MagicAttackIndex] = cached.MagicAttack;
        fields[LegacyPlayerStatusPacket.MagicPercentIndex] = cached.MagicPercent;
        fields[LegacyPlayerStatusPacket.SkillCStrikeIndex] = cached.SkillCStrike;
        fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex] = cached.MagicDamageIncrease;
        return new LegacyPlayerStatusPacket(fields);
    }
}
