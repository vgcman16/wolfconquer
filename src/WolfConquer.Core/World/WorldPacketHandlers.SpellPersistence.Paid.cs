using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private enum LegacySpellLearningCurrency { Money, ConquerPoints }

    private static bool TryCommitLegacyPaidSpellLearning(WorldPacketHandlerContext context, WorldSession expected,
        PlayerSpellSnapshotEntry learned, LegacySpellLearningCurrency currency, long cost, long minimumLevel,
        bool requiresTrojanEpicWeapon, CancellationToken cancellationToken,
        out LegacyPaidSpellLearning? committed, out string reason)
    {
        committed = null; reason = "paidSpellOwnerUnavailable";
        if (context.Trades is not { } trades || context.Sessions is not { } sessions) return false;
        LegacyPaidSpellLearning? plan = null;
        LegacySpellFileOwner? capturedOwner = null;
        lock (trades.SyncRoot)
        {
            try
            {
                if (!LegacySpellFileOwner.TryCapture(context, expected, paid: true, out var owner, out reason)) return false;
                capturedOwner = owner;
                plan = new(owner!, learned, currency, cost, minimumLevel, requiresTrojanEpicWeapon);
                var current = plan;
                var failure = "paidSpellSessionChanged";
                var success = sessions.TryCommitSession(context.Connection, expected, current.Updated,
                    () => current.TryPersist(cancellationToken, out failure), out var final);
                if (success)
                {
                    if (final is null) throw new LegacyCommittedSpellPublicationException();
                    current.Updated = final; committed = current; reason = string.Empty; return true;
                }
                reason = failure;
                if (current.DurableCommitted || failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal))
                {
                    reason = "ordinarySpellRecoveryRequired:paidSpellOutcomeUnproven:" + failure;
                    owner!.Recovery.Block(reason);
                }
                return false;
            }
            catch (WorldSessionPublicationException published)
            {
                FenceSpellPublishedOutcome(capturedOwner, published);
                throw;
            }
            catch (LegacyCommittedSpellPublicationException)
            {
                reason = "ordinarySpellRecoveryRequired:paidSpellPublicationUnproven";
                context.TradeRecovery?.Block(reason); return false;
            }
            catch (Exception error) when (IsSpellMutationPreparationFailure(error))
            {
                if (plan?.DurableCommitted == true)
                {
                    reason = "ordinarySpellRecoveryRequired:paidSpellPublication:" + error.GetType().Name;
                    context.TradeRecovery?.Block(reason);
                }
                else reason = error is LegacySpellMutationRefusalException ? error.Message :
                    "paidSpellPreparation:" + error.GetType().Name;
                return false;
            }
        }
    }

    private sealed partial class LegacyPaidSpellLearning
    {
        private readonly LegacySpellFileOwner owner;
        private readonly LegacySpellMutationInputs inputs;
        private readonly LegacyTradeJournalChange[] changes;
        private readonly LegacyPreparedSpellFileWrite spellWrite;
        private readonly string characterPath, spellsPath;
        private readonly byte[][] grantFrames;
        internal WorldSession Updated { get; set; }
        internal bool DurableCommitted { get; private set; }

        internal LegacyPaidSpellLearning(LegacySpellFileOwner owner, PlayerSpellSnapshotEntry learned,
            LegacySpellLearningCurrency currency, long cost, long minimumLevel, bool requiresTrojanEpicWeapon)
        {
            this.owner = owner;
            if (!Enum.IsDefined(currency) || cost <= 0 || minimumLevel < 0 || learned is null || learned.Id == 0)
                throw InvalidSpellMutation();
            inputs = new(owner.Expected, captureItems: requiresTrojanEpicWeapon);
            var before = inputs.Stable.Character;
            if (!LegacySecondaryPasswordRuntime.IsAdmitted(owner.Context.Connection, inputs.Stable))
                throw new LegacySpellMutationRefusalException("paidSpellPinChanged");
            if (before.Level < minimumLevel || inputs.Spells.Rows.Any(row => row.Id == learned.Id) ||
                requiresTrojanEpicWeapon && !HasLegacyEquippedTrojanEpicWeapon(inputs.Stable.PlayerItems))
                throw new LegacySpellMutationRefusalException("paidSpellEligibilityChanged");
            if (!LegacyTradeEscrowClaims.TryRead(before.Attributes, out var claim, out _) || claim is not null)
                throw new LegacySpellMutationRefusalException("paidSpellEscrowUnresolved");
            var balance = currency == LegacySpellLearningCurrency.Money ? before.Money : before.ConquerPoints;
            if (balance < cost) throw new LegacySpellMutationRefusalException("paidSpellFundsChanged");
            var after = currency == LegacySpellLearningCurrency.Money
                ? before with { Money = checked(balance - cost) } : before with { ConquerPoints = checked(balance - cost) };
            var attributes = new Dictionary<string, string>(inputs.Stable.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(after)) attributes[pair.Key] = pair.Value;
            after = after with { Attributes = attributes };
            var spells = LegacySkillBookUse.AddOrUpdateSpell(inputs.Stable.PlayerSpells, learned);
            Updated = owner.Expected with { Character = after,
                PlayerSpells = new(new LegacyPerformerSpellRows(new LegacyPerformerSpellSource(spells.Spells).Rows)) };
            var scalar = currency == LegacySpellLearningCurrency.Money ? LegacyMsgUpdateDataType.Money : LegacyMsgUpdateDataType.ConquerPoints;
            grantFrames = [new LegacyMsgUpdatePacket(checked((uint)after.Uid),
                [new(scalar, checked((ulong)(balance - cost)))]).ToFrameBytes(), LegacySpellPacket.FromSnapshot(learned).ToFrameBytes()];
            spellWrite = owner.Context.Sessions!.PrepareSpellFileWrite(owner.Context.Connection, owner.Expected,
                owner.Recovery, owner.UsersRoot, owner.SpellsRoot);
            spellWrite.PrepareImage(Updated.PlayerSpells);
            (characterPath, spellsPath, changes) = PrepareImages();
            inputs.ArmPlainProof();
            if (!owner.IsCurrent()) throw new LegacySpellMutationRefusalException("paidSpellOwnerChanged");
        }

        internal async ValueTask<bool> PublishGrantAsync(CancellationToken cancellationToken)
        {
            foreach (var frame in grantFrames)
            {
                if (!owner.DeliveryIsCurrent(Updated)) return false;
                await owner.Context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                if (!owner.DeliveryIsCurrent(Updated)) return false;
            }
            return true;
        }
    }

    private static Task LogLegacyPaidSpellRefusalAsync(WorldPacketHandlerContext context, string reason) =>
        context.Log.WriteLineAsync(reason.StartsWith("ordinarySpellRecoveryRequired:", StringComparison.Ordinal)
            ? "WorldServer held paid spell learning for recovery; durable outcome is not an unchanged refusal. reason=" + reason
            : "WorldServer refused paid spell learning before commitment; currency and spells unchanged. reason=" + reason);
}
