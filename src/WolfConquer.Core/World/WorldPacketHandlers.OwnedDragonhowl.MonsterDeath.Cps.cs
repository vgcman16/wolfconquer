using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal enum DragonhowlDeathCpsKind { Random, Boss }

    // Recorded policy/RNG result and final bytes, not a kill or wallet permit.
    // Only the Store's registered death/step lineage can consume this DATA.
    internal sealed class DragonhowlDeathCpsData
    {
        internal WorldSession Before { get; }
        internal WorldSession After { get; }
        internal uint Amount { get; }
        internal DragonhowlDeathCpsKind Kind { get; }
        internal int? RatePercent { get; }
        internal int? RateRoll { get; }
        private readonly byte[][] frames;
        private readonly LegacyNativeAttributeMapProof beforeFields, afterFields, paid;

        private DragonhowlDeathCpsData(WorldSession before, WorldSession after, uint amount,
            DragonhowlDeathCpsKind kind, int? ratePercent, int? rateRoll, byte[][] frames,
            LegacyNativeAttributeMapProof beforeFields)
        {
            Before = before; After = after; Amount = amount; Kind = kind;
            RatePercent = ratePercent; RateRoll = rateRoll; this.frames = frames;
            this.beforeFields = beforeFields;
            afterFields = LegacyNativeAttributeMapProof.CaptureRaw(after.Character.Attributes);
            paid = LegacyNativeAttributeMapProof.CaptureRaw(before.Character.PaidNobility);
        }

        internal static DragonhowlDeathCpsData Prepare(WorldSession before, LegacyMonsterInstance monster,
            Monster family, DragonhowlDeathCpsKind kind)
        {
            if (before.AccountId is <= 0 or > uint.MaxValue || before.Character.Uid is <= 0 or > uint.MaxValue ||
                before.Character.ConquerPoints is < 0 or > 2_000_000_000L || family.Id < 0 || monster.MapId < 0)
                throw new InvalidDataException("ownedDragonhowlDeathCpsInputInvalid");
            var proof = LegacyNativeAttributeMapProof.CaptureRaw(before.Character.Attributes);
            var fields = new Dictionary<string, string>(proof.CapturedComparer);
            foreach (var pair in proof.Fields) fields.Add(pair.Key, pair.Value);
            var cpKeys = fields.Keys.Where(key => key.Equals("ConquerPoints", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (cpKeys.Length != 1 || !long.TryParse(fields[cpKeys[0]], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var rawCp) || rawCp != before.Character.ConquerPoints)
                throw new InvalidDataException("ownedDragonhowlDeathCpsWalletAmbiguous");

            uint amount = 0; int? rate = null, roll = null;
            var messages = new List<byte[]>();
            if (kind == DragonhowlDeathCpsKind.Random)
            {
                // Only fields actually read by this policy are folded. Opaque
                // case-distinct fields retain their original comparer/content.
                string[] used = ["VIPLevel", RuntimeAutoHuntAttribute,
                    RuntimeLegacyMonsterDeathConquerPointRatePercentAttribute,
                    RuntimeLegacyMonsterDeathConquerPointRateRollAttribute,
                    RuntimeLegacyMonsterDeathConquerPointRollAttribute];
                var policyFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in proof.Fields)
                    if (used.Contains(pair.Key, StringComparer.OrdinalIgnoreCase) && !policyFields.TryAdd(pair.Key, pair.Value))
                        throw new InvalidDataException("ownedDragonhowlDeathCpsPolicyAmbiguous");
                var policyCharacter = before.Character with { Attributes = policyFields };
                var vip = ReadAttributeUInt64(policyFields, "VIPLevel", "VipLevel");
                var reward = LegacyBossDeathRewardPolicy.GetMonsterDeathRandomConquerPointReward(
                    checked((uint)family.Id), checked((uint)monster.MapId), monster.Boss,
                    ReadAttributeBool(policyFields, RuntimeAutoHuntAttribute), vip > int.MaxValue ? int.MaxValue : (int)vip);
                rate = ReadLegacyMonsterDeathRandomConquerPointRatePercent(policyCharacter, reward);
                // Source draws the rate roll even when the configured rate is zero.
                roll = ReadLegacyMonsterDeathRandomConquerPointRateRoll(policyCharacter);
                if (CheckLegacyMonsterDeathRandomConquerPointRate(rate.Value, roll.Value))
                {
                    amount = ReadLegacyMonsterDeathRandomConquerPoints(policyCharacter, reward);
                    if (reward.FormatSystemMessage(amount) is { } message)
                        messages.Add(LegacyMsgMessagePacket.SystemTalk(message).ToFrameBytes());
                }
            }
            else if (kind == DragonhowlDeathCpsKind.Boss)
            {
                if (LegacyBossDeathRewardPolicy.TryGetConquerPointReward(checked((uint)family.Id),
                    checked((uint)monster.MapId), monster.Boss, out var reward))
                {
                    amount = checked((uint)reward.TotalConquerPoints);
                    var message = LegacyMsgMessagePacket.SystemTalk(reward.FormatLastHitterMessage(monster.Name)).ToFrameBytes();
                    for (int index = 0; index < reward.InvocationCount; index++) messages.Add(message);
                }
            }
            else throw new InvalidDataException("ownedDragonhowlDeathCpsKindInvalid");

            long total = checked(before.Character.ConquerPoints + amount);
            if (total > 2_000_000_000L) throw new InvalidDataException("ownedDragonhowlDeathCpsWalletOverflow");
            WorldSession after = before;
            if (amount != 0)
            {
                fields[cpKeys[0]] = total.ToString(CultureInfo.InvariantCulture);
                after = before with { Character = before.Character with { ConquerPoints = total, Attributes = fields } };
                messages.Insert(0, new LegacyMsgUpdatePacket(checked((uint)before.Character.Uid),
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)total))]).ToFrameBytes());
            }
            if (!proof.IsCurrentPlain()) throw new InvalidDataException("ownedDragonhowlDeathCpsInputsChanged");
            return new(before, after, amount, kind, rate, roll, messages.ToArray(), proof);
        }

        internal bool InputsAreCurrentPlain() => beforeFields.IsCurrentPlain() && afterFields.IsCurrentPlain() && paid.IsCurrentPlain();
        internal bool MatchesBefore(WorldSession current) => InputsAreCurrentPlain() && Matches(current, Before, beforeFields);
        internal bool MatchesAfter(WorldSession current) => InputsAreCurrentPlain() && Matches(current, After, afterFields);
        private static bool Matches(WorldSession current, WorldSession projected, LegacyNativeAttributeMapProof fields)
        {
            if ((current with { Character = projected.Character }) != projected ||
                (current.Character with { Attributes = projected.Character.Attributes }) != projected.Character) return false;
            var actual = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes);
            return ReferenceEquals(actual.CapturedComparer, fields.CapturedComparer) &&
                actual.Fields.SequenceEqual(fields.Fields) && actual.IsCurrentPlain();
        }

        internal async ValueTask<bool> PublishAsync(OwnedDragonhowlPreparation plan,
            WorldSessionStore.OwnedDragonhowlMonsterDeath death, WorldSessionStore.OwnedDragonhowlDeathCps step,
            CancellationToken cancellationToken)
        {
            if (!plan.Store.TryBeginDragonhowlDeathCpsOutput(plan, death, step, this, out var current)) return false;
            foreach (var frame in frames)
            {
                if (!plan.Store.IsDragonhowlDeathCpsOutputCurrent(plan, death, step, current)) return false;
                await plan.Context.Connection.SendAsync(frame.ToArray(), cancellationToken).ConfigureAwait(false);
                if (!plan.Store.IsDragonhowlDeathCpsOutputCurrent(plan, death, step, current)) return false;
            }
            return plan.Store.CompleteDragonhowlDeathCpsOutput(plan, death, step, current);
        }
    }
}
