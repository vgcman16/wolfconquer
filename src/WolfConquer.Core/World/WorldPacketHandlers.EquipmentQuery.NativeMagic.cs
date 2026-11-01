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
            return capturedQuerySource is not null && LegacyNativeMagicQueryClient.TryBeginQuery(Context.Sessions, Context.Connection,
                Request.Scope.OwnerUid, Request.Scope.OwnerNativeClientIdentity, out queryEpoch, capturedQuerySource);
        }

        private bool NativeMagic(Func<LegacyNativeMagicQueryFields, LegacyNativeMagicQueryFields> update) =>
            Refresh() && LegacyNativeMagicQueryClient.Update(
                Context.Sessions, Context.Connection, Request.Scope.OwnerUid, update,
                Request.Scope.OwnerNativeClientIdentity, queryEpoch);

        private LegacyNativeMagicQueryFields NativeMagicCurrent =>
            LegacyNativeMagicQueryClient.Read(Context.Sessions, Context.Connection,
                Request.Scope.OwnerUid, Request.Scope.OwnerNativeClientIdentity);

        private bool AddNativeMagic(LegacyNativeMagicQueryFields add) =>
            NativeMagic(current => LegacyEquipmentMagicArithmetic.Add(current, add));

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
                if (!NativeMagic(current => LegacyEquipmentMagicArithmetic.Witchery(current, witchery))) return false;
            }
            if (Context.WorldData is { } chantData &&
                TryGetLegacyEligibleEquippedRuneLevel(Session, chantData, "Nature`sChant", out var chant))
            {
                if (!NativeMagic(current => LegacyEquipmentMagicArithmetic.NatureChant(current, chant)))
                    return false;
            }
            return true;
        }

        private bool NativeMagicBeforeGear()
        {
            if (!Refresh()) return false;
            if (!NativeMagic(current => EquipmentQueryMagicBeforeGear(current, Session)))
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
                    Request.Scope.OwnerNativeClientIdentity, queryEpoch)) return false;
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

    private static LegacyNativeMagicQueryFields EquipmentQueryMagicBeforeGear(LegacyNativeMagicQueryFields current, WorldSession source)
    {
        var wardrobe = EnumerateLegacyWardrobeItems(source.PlayerItems).ToArray();
        bool mount = wardrobe.Where(item => ItemType.GetLegacyEquipmentPosition(item.ItemId) == 17)
            .Select(item => item.Uid).Distinct().Count() > 100;
        bool garment = wardrobe.Where(item => ItemType.GetLegacyEquipmentPosition(item.ItemId) == 9)
            .Select(item => item.Uid).Distinct().Count() > 100;
        return LegacyEquipmentMagicArithmetic.BeforeGear(current, mount, garment);
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
