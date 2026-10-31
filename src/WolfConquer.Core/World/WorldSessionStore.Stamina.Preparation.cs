using System.Globalization;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    private sealed class PreparedStamina : IDisposable
    {
        private readonly WorldSessionStore store;
        private readonly WorldPacketHandlerContext context;
        private readonly SessionHolder holder;
        private readonly WorldSession expected;
        private readonly LegacyOwnedStaminaRuntime.Readiness readiness;
        private readonly WorldPacketHandlers.StaminaInputs inputs;
        private readonly LegacyNativeStatusFlagsBigVector32.StaminaFlags flags;
        private readonly LegacyNativeMagicQueryClient.MonkCache cache;
        private readonly LegacyStaminaMonkMetadata? metadata;
        private readonly JoinedWindWalker? fanOwner;
        internal PreparedSessionSpellPublication Native { get; }
        internal LegacyOwnedStaminaRuntime.Readiness.Step Step { get; }
        internal StaminaPublication Output { get; }

        private PreparedStamina(WorldSessionStore store, WorldPacketHandlerContext context, SessionHolder holder,
            WorldSession expected, LegacyOwnedStaminaRuntime.Readiness readiness, WorldPacketHandlers.StaminaInputs inputs,
            LegacyNativeStatusFlagsBigVector32.StaminaFlags flags, LegacyNativeMagicQueryClient.MonkCache cache,
            LegacyStaminaMonkMetadata? metadata, JoinedWindWalker? fanOwner, PreparedSessionSpellPublication native,
            LegacyOwnedStaminaRuntime.Readiness.Step step, StaminaPublication output)
        { this.store = store; this.context = context; this.holder = holder; this.expected = expected; this.readiness = readiness;
          this.inputs = inputs; this.flags = flags; this.cache = cache; this.metadata = metadata; this.fanOwner = fanOwner;
          Native = native; Step = step; Output = output; }

        internal static PreparedStamina? TryPrepare(WorldSessionStore store, WorldPacketHandlerContext context,
            SessionHolder holder, WorldSession expected, LegacyOwnedStaminaRuntime.Readiness readiness)
        {
            var binding = readiness.Binding;
            var entryTime = readiness.ReadLocalNow(); // Source Buffers captured-now; never a request timestamp.
            var inputs = new WorldPacketHandlers.StaminaInputs(expected);
            if (!LegacyNativeMagicQueryClient.TryReadMonkCache(binding, out var cache) || cache is null ||
                !binding.Player.BitVector.TryCaptureStaminaFlags(out var flags) || flags is null) return null;
            LegacyStaminaMonkMetadata? metadata = null;
            if (cache.UsesMonkEpic)
            {
                var learned = inputs.Stable.PlayerSpells.Spells.Where(value => value.Id == 12_560).ToArray();
                if (learned.Length > 1) throw new InvalidDataException("staminaMonkLearnedAmbiguous");
                if (learned.Length == 1) metadata = new(binding.ItemCatalog ??
                    throw new InvalidDataException("staminaMonkCatalogMissing"), checked((ushort)learned[0].Level));
            }
            JoinedWindWalker? fan = store.WindWalkerOwnerForStamina(binding, flags);
            var gateTime = readiness.ReadLocalNow(); // Separate current due-gate read, same configured authority.
            if (gateTime < entryTime) return null;
            if (!LegacyStaminaRegenerationRules.TryPrepare(inputs.Stable, flags, fan is not null, metadata?.Bonus ?? 0,
                    entryTime, gateTime, readiness.StaminaStamp, readiness.FanStamp, out var result)) return null;
            var staminaTime = readiness.ReadLocalNow();
            var fanTime = result.Fan ? readiness.ReadLocalNow() : readiness.FanStamp;
            // Distinct source-phase reads are retained. A wall-clock rollback
            // within this operation cannot publish an earlier cooldown stamp
            // and manufacture an accelerated repeat grant.
            if (staminaTime < gateTime || result.Fan && fanTime < staminaTime) return null;
            var step = readiness.Prepare(result.Stamina, result.Fan, result.Effect, staminaTime, fanTime);
            var attributes = new Dictionary<string, string>(inputs.Stable.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Stamina"] = result.Stamina.ToString(CultureInfo.InvariantCulture) };
            // Keep the original native item/spell snapshot identities at this
            // boundary; the concrete prepared Store graph owns canonicalizing.
            var proposed = expected with { Character = inputs.Stable.Character with { Attributes = attributes } };
            if (!LegacyMailboxPackageMutationFence.CanMutateSession(store.MailboxPackages, expected, proposed)) return null;
            var native = new PreparedSessionSpellPublication(store, context.Connection, holder, expected, proposed);
            try
            {
                if (native.HitpointsWrite is not null || LegacyStaminaState.RequireCurrent(native.FinalSession.Character) != step.Stamina ||
                    WolfConquer.Core.Data.CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(expected.Character, native.FinalSession.Character))
                    throw new InvalidDataException("staminaTransientProjectionChangedPersistentState");
                var output = StaminaPublication.Create(store, context, readiness, step, native.FinalSession, flags);
                return new(store, context, holder, expected, readiness, inputs, flags, cache, metadata, fan, native, step, output);
            }
            catch { native.Dispose(); throw; }
        }

        internal bool IsCurrentPlain() => readiness.Frame.IsStaminaReadinessCurrent(readiness) &&
            ReferenceEquals(holder.Read(), expected) && store.BindingIsCurrentPlain(readiness.Binding, holder, expected) &&
            readiness.Frame.IsWindWalkerContext(store, context, expected) && !readiness.Binding.Client.Fake &&
            readiness.Binding.Player.View is { } view && ReferenceEquals(view.Owner, readiness.Binding.Client) &&
            inputs.IsCurrentPlain() && flags.IsCurrentPlain(readiness.Binding.Player.BitVector) && cache.IsCurrentPlain(readiness.Binding) &&
            (metadata is null || metadata.IsCurrentPlain()) && Step.IsCurrentPlain() &&
            (fanOwner is null || store.WindWalkerOwnerForStaminaPlain(readiness.Binding, flags, fanOwner));
        public void Dispose() => Native.Dispose();
    }

    private JoinedWindWalker? WindWalkerOwnerForStamina(LegacyOrdinarySpellBinding binding,
        LegacyNativeStatusFlagsBigVector32.StaminaFlags flags) => flags.Fan.Present &&
        WindWalkerOwners.TryGetValue(binding.Player, out var slot) && slot.Owner is { } joined &&
        WindWalkerBindingCurrentPlain(joined, binding.Player) ? joined : null;

    private bool WindWalkerOwnerForStaminaPlain(LegacyOrdinarySpellBinding binding,
        LegacyNativeStatusFlagsBigVector32.StaminaFlags flags, JoinedWindWalker joined) => flags.Fan.Present &&
        WindWalkerBindingCurrentPlain(joined, binding.Player) &&
        WindWalkerOwners.TryGetValue(binding.Player, out var slot) && ReferenceEquals(slot.Owner, joined);
}
