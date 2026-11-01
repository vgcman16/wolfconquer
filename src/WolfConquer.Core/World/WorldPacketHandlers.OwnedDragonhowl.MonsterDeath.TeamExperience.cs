using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Inert calculation inputs/results. Only the Store's registered death
    // lineage may later associate these with an actual committed kill.
    internal sealed record DragonhowlTeamExperienceMember(WorldPacketHandlerContext Context,
        LegacyOrdinarySpellBinding Binding, WorldSession CalculatedBefore,
        DragonhowlLevelExperienceData? Previous = null);

    internal sealed record DragonhowlTeamExperienceRecipient(DragonhowlTeamExperienceMember Member,
        int Share, LegacyExperienceGainInputs Inputs, LegacyExperienceGainResult Result,
        DragonhowlLevelExperienceData? Levels)
    {
        internal ulong Gain => Result.AppliedExperienceGain;
        internal WorldSession ProjectedAfter => Levels?.ProjectedAfter ?? Member.CalculatedBefore;
    }

    internal sealed class DragonhowlMonsterTeamExperienceData
    {
        private readonly WorldPacketHandlerContext context;
        private readonly LegacyOrdinarySpellBinding killer;
        private readonly ObservedMember[] observed;
        private readonly RecipientInputs[] recipientInputs;
        internal TeamSessionStateStore.CombatExperienceRoster Roster { get; }
        internal IReadOnlyList<DragonhowlTeamExperienceRecipient> Recipients { get; }
        internal int ComputedShare { get; }
        internal bool RequiresDerivedSpellWrite => Recipients.Any(row => row.Levels?.RequiresDerivedSpellWrite == true);

        private DragonhowlMonsterTeamExperienceData(WorldPacketHandlerContext context,
            LegacyOrdinarySpellBinding killer, WorldSession calculatedKiller, LegacyMonsterInstance monster,
            TeamSessionStateStore.CombatExperienceRoster roster, DragonhowlTeamExperienceMember[] members)
        {
            this.context = context; this.killer = killer; Roster = roster;
            if (context.TeamStates is null || !ReferenceEquals(context.Connection, killer.Connection) ||
                !ReferenceEquals(context.Sessions, killer.Store) || context.WorldData is null ||
                members.Length != roster.Members.Count || members.Length == 0 || !RosterCurrent()) throw Invalid();
            observed = new ObservedMember[members.Length];
            for (int index = 0; index < members.Length; index++)
            {
                var supplied = members[index];
                var row = roster.Members[index];
                if (!ReferenceEquals(row.Connection, supplied.Binding.Connection) || row.Uid != supplied.Binding.Uid ||
                    members.Take(index).Any(previous => ReferenceEquals(previous.Binding.Connection, row.Connection) ||
                        previous.Binding.Uid == row.Uid)) throw Invalid();
                observed[index] = new(context, supplied);
            }
            var killerMember = observed.SingleOrDefault(row => ReferenceEquals(row.Source.Binding, killer));
            if (killerMember is null || !ReferenceEquals(killerMember.Source.CalculatedBefore, calculatedKiller)) throw Invalid();
            ComputedShare = observed.Length <= 1 ? 0 : LegacyMonsterTeamExperienceShare.CalculateComputedShare(
                new(checked((int)monster.MaxLife), monster.Level, monster.MapId, monster.X, monster.Y),
                observed.Select(row => row.ShareMember));
            uint spouse = ComputedShare == 0 ? 0 : ReadAttributeUInt32(
                PolicyFields(killerMember.CalculatedFields, ["SpouseUID", "SpouseID"]),
                "SpouseUID", "SpouseUid", "SpouseID", "SpouseId");
            var results = new List<DragonhowlTeamExperienceRecipient>();
            var policies = new List<RecipientInputs>();
            // Actual roster order is the source award order. Ineligible rows
            // still remain observed inputs to the newbie/recipient calculation.
            foreach (var row in observed)
            {
                int share = LegacyMonsterTeamExperienceShare.CalculateRecipientAmount(ComputedShare,
                    row.ShareMember, killerMember.ShareMember, spouse);
                if (share == 0) continue;
                var policy = new RecipientInputs(row, share);
                var result = CheckedTeamExperience(policy.Inputs);
                DragonhowlLevelExperienceData? levels = null;
                if (result.AppliedExperienceGain != 0 &&
                    (!DragonhowlLevelExperienceData.TryPrepare(row.Source.Context, row.Source.Binding,
                        row.Source.CalculatedBefore, result.AppliedExperienceGain, out levels, row.Source.Previous) || levels is null)) throw Invalid();
                policies.Add(policy);
                results.Add(new(row.Source, share, policy.Inputs, result, levels));
            }
            recipientInputs = policies.ToArray();
            Recipients = Array.AsReadOnly(results.ToArray());
            if (!IsCurrentPlain()) throw Invalid();
        }

        internal static bool TryPrepare(WorldPacketHandlerContext context, LegacyOrdinarySpellBinding killer,
            WorldSession calculatedKiller, LegacyMonsterInstance monster,
            TeamSessionStateStore.CombatExperienceRoster roster, DragonhowlTeamExperienceMember[] members,
            out DragonhowlMonsterTeamExperienceData? data)
        {
            data = null;
            try
            {
                data = new(context, killer, calculatedKiller, monster, roster, members.ToArray());
                return true;
            }
            catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                OverflowException or FormatException or IOException or UnauthorizedAccessException or NotSupportedException)
            { return false; }
        }

        // Pre-debit currentness only: live holder replacements after the real
        // proc begins require the Store's actual writer/continuation receipts.
        internal bool IsCurrentPlain() => RosterCurrent() && observed.All(row => row.IsCurrentPlain()) &&
            recipientInputs.All(row => row.IsCurrentPlain()) &&
            Recipients.All(row => row.Levels is null || row.Levels.IsCurrentPlain()) && RosterCurrent();

        internal bool RecordedInputsAreCurrent() => observed.All(row => row.RecordedInputsAreCurrent()) &&
            recipientInputs.All(row => row.RecordedInputsAreCurrent()) &&
            Recipients.All(row => row.Levels is null || row.Levels.RecordedInputsAreCurrent());

        private bool RosterCurrent()
        {
            if (!ReferenceEquals(context.Sessions, killer.Store) || context.TeamStates is null) return false;
            using var held = Roster.TryEnter(killer);
            return held is not null && held.IsCurrentPlain();
        }

        private sealed class ObservedMember
        {
            internal readonly DragonhowlTeamExperienceMember Source;
            internal readonly LegacyMonsterTeamExperienceShareMember ShareMember;
            private readonly WorldSession live;
            private readonly LegacyNativeAttributeMapProof liveFields;
            internal readonly LegacyNativeAttributeMapProof CalculatedFields;

            internal ObservedMember(WorldPacketHandlerContext ownerContext, DragonhowlTeamExperienceMember source)
            {
                Source = source;
                var binding = source.Binding; var before = source.CalculatedBefore; var recipient = source.Context;
                if (!ReferenceEquals(binding.Store, ownerContext.Sessions) ||
                    !ReferenceEquals(recipient.Sessions, ownerContext.Sessions) ||
                    !ReferenceEquals(recipient.WorldData, ownerContext.WorldData) ||
                    !ReferenceEquals(recipient.TeamStates, ownerContext.TeamStates) ||
                    !ReferenceEquals(recipient.TradeOperations, ownerContext.TradeOperations) ||
                    !ReferenceEquals(recipient.FrameOperationLease, ownerContext.FrameOperationLease) ||
                    !ReferenceEquals(recipient.Connection, binding.Connection) ||
                    !ReferenceEquals(before.NativeGameClient, binding.Client) || before.AccountId != binding.AccountId ||
                    before.Character.Uid != binding.Uid || before.Character.Level is < 1 or > 140 ||
                    !binding.Store.TryGet(binding.Connection, out var actual) ||
                    !binding.Store.IsLevelUpRebuildOwnerHeld(binding, actual, recipient)) throw Invalid();
                live = actual;
                if (!ReferenceEquals(before.PlayerItems, live.PlayerItems)) throw Invalid();
                liveFields = LegacyNativeAttributeMapProof.CaptureRaw(live.Character.Attributes);
                CalculatedFields = LegacyNativeAttributeMapProof.CaptureRaw(before.Character.Attributes);
                ShareMember = CreateLegacyMonsterTeamExperienceShareMember(before.Character with
                    { Attributes = PolicyFields(CalculatedFields, ["StatusFlags", "BitVector"]) });
                if (!IsCurrentPlain()) throw Invalid();
            }

            internal bool IsCurrentPlain() => liveFields.IsCurrentPlain() && CalculatedFields.IsCurrentPlain() &&
                Source.Binding.Store.IsLevelUpRebuildOwnerHeld(Source.Binding, live, Source.Context);
            internal bool RecordedInputsAreCurrent() => liveFields.IsCurrentPlain() && CalculatedFields.IsCurrentPlain();
        }

        private sealed class RecipientInputs
        {
            private readonly ObservedMember member;
            private readonly int share;
            private readonly WorldSession policySession;
            private readonly LegacyItemSaveGraph items;
            private readonly LegacyOwnedPhysicalMetadataWitness metadata;
            internal readonly LegacyExperienceGainInputs Inputs;

            internal RecipientInputs(ObservedMember member, int share)
            {
                this.member = member; this.share = share;
                var source = member.Source;
                if (!LegacyItemSaveGraph.TryCapture(source.CalculatedBefore.PlayerItems, out var image) || image is null)
                    throw Invalid();
                items = image;
                source.Binding.Items.RequireCurrentBeforePreparation();
                policySession = source.CalculatedBefore with { Character = source.CalculatedBefore.Character with
                    { Attributes = PolicyFields(member.CalculatedFields, GainFields) } };
                LegacyDragonhowlExperienceRateInputs.RequireSelected(policySession.Character.Attributes);
                metadata = LegacyOwnedPhysicalMetadataWitness.CaptureDragonhowlTeamExperience(
                    source.Binding, source.Context.WorldData ?? throw Invalid());
                // The source's explicit SoulChant override bypasses item lookup.
                if (!TryReadUInt32Attribute(policySession.Character.Attributes, out _,
                    "SoulChantLevel", "SoulChantRuneLevel", "RuneSoulChantLevel"))
                    foreach (var rune in source.CalculatedBefore.PlayerItems.RuneItems)
                    {
                        if (!IsLegacyWornRunePosition(rune.Position)) continue;
                        metadata.UseItem(rune.ItemId);
                        if (TryGetLegacyItemType(source.Context.WorldData!.ItemsById, rune.ItemId, out var row) &&
                            ContainsLegacyItemText(row, "SoulChant")) break;
                    }
                Inputs = CreateLegacyExperienceGainInputs(source.Context, policySession, share);
                if (!IsCurrentPlain()) throw Invalid();
            }

            internal bool IsCurrentPlain()
            {
                if (!member.IsCurrentPlain() || !items.IsCurrent(member.Source.CalculatedBefore.PlayerItems) ||
                    !metadata.IsCurrentPlain()) return false;
                member.Source.Binding.Items.RequireCurrentBeforePreparation();
                return CreateLegacyExperienceGainInputs(member.Source.Context, policySession, share) == Inputs;
            }

            internal bool RecordedInputsAreCurrent() => member.RecordedInputsAreCurrent() &&
                items.IsCurrent(member.Source.CalculatedBefore.PlayerItems) && metadata.IsCurrentPlain() &&
                CreateLegacyExperienceGainInputs(member.Source.Context, policySession, share) == Inputs;
        }

        private static readonly string[] GainFields = ["StatusFlags", "BitVector", "CursedTimer",
                "SoulChantLevel", "SoulChantRuneLevel", "RuneSoulChantLevel", "UserExpRate", "ServerExperienceRate",
                "ExperienceRate", "MonsterKillExperienceRate", "MaximumServerExperienceRate", "MaximumMonsterKillExperienceRate",
                "DExpTime", "DoubleExperienceTime", "DoubleExpTime", "RateExp", "DoubleExperienceRate", "DExpRate",
                "HeavenBlessing", "HeavensBlessing"];

        private static Dictionary<string, string> PolicyFields(LegacyNativeAttributeMapProof captured, string[] used)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in captured.Fields)
                if (used.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) && !fields.TryAdd(pair.Key, pair.Value)) throw Invalid();
            return fields;
        }

        private static LegacyExperienceGainResult CheckedTeamExperience(LegacyExperienceGainInputs inputs)
        {
            // Keep the actual source policy as the result producer, but reject
            // its unchecked double-to-UInt64 domain before any future credit.
            if (!double.IsFinite(inputs.BaseAmount) || inputs.BaseAmount < 0 ||
                !double.IsFinite(inputs.ServerExperienceRate) || inputs.ServerExperienceRate < 0 ||
                !double.IsFinite(inputs.DoubleExperienceRate) || inputs.DoubleExperienceRate < 0) throw Invalid();
            if (inputs.CursedTimer <= LegacyExperienceGainPolicy.CursedTimerSuppressionThreshold &&
                inputs.PlayerLevel < LegacyExperienceGainPolicy.MaximumExperienceLevel)
            {
                double amount = inputs.BaseAmount;
                if (inputs.SoulChantEquipped)
                    amount -= amount * (LegacyExperienceGainPolicy.SoulChantBaseReductionPercent +
                        inputs.SoulChantItemLevel * LegacyExperienceGainPolicy.SoulChantItemLevelReductionPercent) / 100;
                RequireFiniteAmount(amount);
                amount *= inputs.ServerExperienceRate; RequireFiniteAmount(amount);
                amount += amount * inputs.RainbowGemPercent / 100; RequireFiniteAmount(amount);
                if (inputs.DoubleExperienceTime != 0) { amount *= inputs.DoubleExperienceRate; RequireFiniteAmount(amount); }
                if (inputs.MapId == LegacyExperienceGainPolicy.ReducedExperienceMapId)
                    amount /= LegacyExperienceGainPolicy.Map1039ExperienceDivisor;
                RequireFiniteAmount(amount);
                // 2^63 is exactly representable; (double)long.MaxValue rounds up.
                if (amount >= 9_223_372_036_854_775_808d) throw Invalid();
            }
            var result = LegacyExperienceGainPolicy.CalculateIncreaseExperience(inputs);
            if (result.AppliedExperienceGain > long.MaxValue || result.OblivionExperienceGain != 0 ||
                result.HuntingBlessingGain != 0) throw Invalid();
            return result;
        }

        private static void RequireFiniteAmount(double amount)
        {
            if (!double.IsFinite(amount) || amount < 0) throw Invalid();
        }

        private static InvalidDataException Invalid() => new("ownedDragonhowlTeamExperienceDataInvalid");
    }
}

internal sealed partial class LegacyOwnedPhysicalMetadataWitness
{
    // Selected-row DATA collection only; no full query, future native witness
    // or combat purpose is issued for a team share that does not change level.
    internal static LegacyOwnedPhysicalMetadataWitness CaptureDragonhowlTeamExperience(
        LegacyOrdinarySpellBinding binding, WorldDataCatalog catalog)
    {
        var result = new LegacyOwnedPhysicalMetadataWitness(binding, catalog, preserveRawAttributes: true);
        if (!result.IsCurrentPlain()) throw Invalid();
        return result;
    }
}
