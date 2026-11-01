using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Future calculation DATA only. This does not prepare native flags, capture
    // a live death target, or create a journal22 participant. Actual execution
    // must bind its terminal query successor through the real death factory.
    internal sealed partial class FuturePlayerDeathData
    {
        private readonly WorldSession before;
        private readonly LegacyPhysicalDeathRankInput[] ranks;
        private readonly OwnedPhysicalDeathDerivedSpellChange[] changes;
        private readonly MagicType[] grants;
        internal WorldSession Session { get; }
        internal bool QueryAdmitted { get; }
        internal LegacyNativeMagicQueryClient.RecordedReceiveValues Query { get; }

        internal static WorldSession ProjectReceivePrefix(WorldSession before, long actorUid) => before with
        {
            Character = WithCharacterAttribute(before.Character, RuntimeLegacyThundercloudSightAttribute,
                CheckedUInt32(actorUid, nameof(actorUid)))
        };

        private FuturePlayerDeathData(WorldSession before, WorldSession after, bool admitted,
            LegacyNativeMagicQueryClient.RecordedReceiveValues query, LegacyPhysicalDeathRankInput[] ranks,
            OwnedPhysicalDeathDerivedSpellChange[] changes, MagicType[] grants)
        {
            this.before = before; Session = after; QueryAdmitted = admitted; Query = query;
            this.ranks = ranks; this.changes = changes; this.grants = grants;
        }

        internal static FuturePlayerDeathData Project(WorldSession actor, WorldSession calculatedTarget,
            FutureEquipmentReceiveData? priorQuery, WorldDataCatalog catalog,
            ILegacySignedMillisecondClock legacyClock, TimeProvider timeProvider, ConquerProtocolProfile profile,
            LegacyNativeMagicQueryClient.RecordedReceiveValues? retainedQuery = null)
        {
            ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(calculatedTarget);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(legacyClock); ArgumentNullException.ThrowIfNull(timeProvider);
            if (profile != ConquerProtocolProfile.VisibleDx8D13 || calculatedTarget.Character.MinHitPoints is <= 0 or > int.MaxValue ||
                actor.Character.Uid is <= 0 or > uint.MaxValue || calculatedTarget.Character.Uid is <= 0 or > uint.MaxValue ||
                HasLegacyPlayerMagicInterserverAttribute(calculatedTarget)) throw InvalidFutureDeath();
            // Closed DATA compatibility: mutable receive HP/status/BT/CA may
            // differ, but no unqueried equipment/stat/gear image is substituted.
            if (priorQuery is not null)
                _ = DragonhowlReceiveCalculationData.Create(profile, profile, targetFuture: priorQuery)
                    .ReadTargetFields(calculatedTarget, catalog);
            var stable = CaptureEquipmentQueryInputs(calculatedTarget);
            var current = stable;
            uint uid = checked((uint)current.Character.Uid);
            var ghostAt = timeProvider.GetUtcNow() + LegacyPlayerDeathState.GhostEffectDelay;
            current = current with { Character = WithCharacterAttribute(current.Character,
                LegacyPlayerDeathState.GhostStampAttribute, ghostAt.ToString("O", CultureInfo.InvariantCulture)) };
            _ = LegacyMsgMessagePacket.SystemTalk(LegacyPlayerDeathState.DeadTalkText).ToFrameBytes();
            current = RemoveDeathDataFlag(current, LegacyBlackNameStatusFlag);
            current = RemoveDeathDataFlag(current, LegacyFatalStrikeFlag);
            var cause = new LegacyNormalPlayerDeathCause(checked((ushort)current.Character.X),
                checked((ushort)current.Character.Y), actor.Character.Uid, actor.Character.Name);
            current = StageLegacyNormalDeathMetadata(current, cause, actor.Character, timeProvider.GetUtcNow());
            if (HasLegacyStatusFlag(current.Character, LegacyRuntimeUpdateTicker.XpListFlag))
                _ = LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(uid, 0).ToFrameBytes();
            current = ClearLegacyNormalDeathRegistrations(current);
            ValidateDeathDataVector(current);

            var querySource = current;
            var metadata = LegacyOwnedPhysicalMetadataWitness.AdmissionQueryData.Capture(querySource, catalog);
            var rebuilt = FutureEquipmentReceiveData.ProjectPlayerDeathQuery(querySource, catalog, legacyClock,
                metadata, profile, out current);
            bool admitted = rebuilt is not null;
            var selectedQuery = rebuilt ?? priorQuery;
            var query = selectedQuery is not null ? new LegacyNativeMagicQueryClient.RecordedReceiveValues(
                selectedQuery.Magic, selectedQuery.JiangHu, selectedQuery.Perfection, selectedQuery.Prestige,
                selectedQuery.MaximumHitpoints, selectedQuery.MaximumMana) : retainedQuery ?? throw InvalidFutureDeath();
            var derivedChanges = admitted ? rebuilt!.Derived.Changes.Select(row =>
                new OwnedPhysicalDeathDerivedSpellChange(row.Id, row.After)).ToArray() : [];
            var selectedRanks = admitted && current.Character.Class / 1000 is 2 or 4 or 7 or 13 or 14
                ? catalog.CombatGearRanks.Select(row => new LegacyPhysicalDeathRankInput(row.CharacterId,
                    Array.AsReadOnly(row.Fields.ToArray()))).ToArray() : [];
            var granted = new List<MagicType>();
            foreach (var change in derivedChanges)
                if (change.After is { } after)
                {
                    metadata.RequireDerivedSpell(change.Id, after.Level);
                    var row = catalog.MagicByTypeId[change.Id].First(row =>
                        LegacyMagicRuntime.IsRecordedDerivedSpellRow(row, change.Id, after.Level));
                    granted.Add(new(row.Id, row.TypeId, row.Name, Array.AsReadOnly(row.Fields.ToArray())));
                }
            // The query's last setter/maximum precedes the actual death's HP=0
            // setter. No UpdateLevel restoration and no second derived query.
            current = current with { Character = LegacyEquipmentQueryBounds.WithHitpoints(current.Character, 0) };
            current = AddLegacyNormalDeathFlag(current);
            ValidateDeathDataVector(current);
            current = RemoveDeathDataFlag(current, LegacyFreezeFlag);
            current = RemoveDeathDataFlag(current, LegacyTyrantAuraStatusFlag);
            current = RemoveDeathDataFlag(current, LegacyFendAuraStatusFlag);
            current = current with { Character = new BoundHitpointsInputs(stable.Character, current.Character).After };
            foreach (var packet in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(uid, query.MaximumHitpoints, 0))
                _ = packet.ToFrameBytes();
            if (!metadata.IsCurrentFor(querySource, catalog)) throw InvalidFutureDeath();
            return new(stable, current, admitted, query, selectedRanks, derivedChanges, granted.ToArray());
        }

        private static WorldSession RemoveDeathDataFlag(WorldSession current, int flag)
        {
            if (!HasLegacyStatusFlag(current.Character, flag)) return current;
            var after = ClearLegacyNormalDeathExplicitFlag(current, flag);
            ValidateDeathDataVector(after);
            return after;
        }

        private static void ValidateDeathDataVector(WorldSession current) =>
            _ = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(checked((uint)current.Character.Uid),
                LegacyPlayerDeathState.CreateStatusFlagVector(current.Character.Attributes,
                    LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength)).ToFrameBytes();

        private static InvalidOperationException InvalidFutureDeath() => new("futurePlayerDeathDataUnavailable");
    }
}
