using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class SobNpcRuntimeStateStore
{
    // Registered target/currentness witness. Only the Store's privately issued
    // proc purpose can consume it; an observed SobNpc is never damage authority.
    internal sealed class OwnedDragonhowlSobTarget
    {
        private readonly SobNpcRuntimeStateStore owner;
        private readonly WorldDataCatalog catalog;
        private readonly IReadOnlyList<SobNpc> adoptedRows;
        private readonly SobNpc birth;
        private readonly SobNpcRuntimeState state;
        private readonly SobStateImage before;
        private readonly string[] fields;
        private CommittedDamage? committed;
        private bool invoked;
        private bool eventReset;

        private OwnedDragonhowlSobTarget(SobNpcRuntimeStateStore owner, WorldDataCatalog catalog,
            SobNpc birth, SobNpcRuntimeState state)
        {
            this.owner = owner; this.catalog = catalog; adoptedRows = catalog.SobNpcs;
            this.birth = birth; this.state = state;
            before = SobStateImage.Capture(state); fields = birth.Fields.ToArray();
        }

        internal uint Hitpoints => before.Hitpoints;

        internal static OwnedDragonhowlSobTarget? TryCapture(SobNpcRuntimeStateStore owner,
            WorldDataCatalog catalog, SobNpc observed)
        {
            if (!IsPlain(catalog.SobNpcs)) return null;
            lock (owner._gate)
            {
                var matching = owner._states.Where(row => row.Key.Id == observed.Id && row.Key.MapId == observed.MapId).ToArray();
                if (matching.Length != 1 || matching[0].Value is not { Birth: { } birth, DeadAt: null, Hitpoints: > 0 } state ||
                    !IsPlain(birth.Fields) || catalog.SobNpcs.Count(row => ReferenceEquals(row, birth)) != 1 ||
                    catalog.SobNpcs.Count(row => row.Id == birth.Id && row.MapId == birth.MapId) != 1 ||
                    Visible(birth, SobStateImage.Capture(state)) != observed) return null;
                var captured = new OwnedDragonhowlSobTarget(owner, catalog, birth, state);
                return captured.BeforeCurrentPlain() ? captured : null;
            }
        }

        internal static bool TryCaptureRegisteredBirth(SobNpcRuntimeStateStore owner, WorldDataCatalog catalog,
            SobNpc selectedBirth, out OwnedDragonhowlSobTarget? target, out SobNpc current)
        {
            target = null; current = null!;
            if (!IsPlain(catalog.SobNpcs) || !IsPlain(selectedBirth.Fields)) return false;
            lock (owner._gate)
            {
                if (catalog.SobNpcs.Count(row => ReferenceEquals(row, selectedBirth)) != 1 ||
                    catalog.SobNpcs.Count(row => row.Id == selectedBirth.Id && row.MapId == selectedBirth.MapId) != 1 ||
                    !owner._states.TryGetValue(SobNpcRuntimeKey.From(selectedBirth), out var state) ||
                    !ReferenceEquals(state.Birth, selectedBirth) || state.DeadAt is not null || state.Hitpoints == 0 ||
                    owner._states.Count(row => row.Key.Id == selectedBirth.Id && row.Key.MapId == selectedBirth.MapId) != 1)
                    return false;
                var captured = new OwnedDragonhowlSobTarget(owner, catalog, selectedBirth, state);
                if (!captured.BeforeCurrentPlain()) return false;
                current = Visible(selectedBirth, captured.before);
                target = captured;
                return true;
            }
        }

        internal bool TryReadCurrent(out SobNpc current)
        {
            current = null!;
            lock (owner._gate)
            {
                if (invoked || !BeforeCurrentPlain()) return false;
                current = Visible(birth, before); return true;
            }
        }

        // Observation only for the real completed-proc repeat lineage. A
        // fresh request must still earn its own permissions, costs and token.
        // HP/reset versions may advance, but a different registered state or
        // adopted birth can never be adopted through this old target witness.
        internal bool TryCaptureRepeatSuccessor(out OwnedDragonhowlSobTarget? next, out SobNpc current)
        {
            next = null; current = null!;
            lock (owner._gate)
            {
                if (!RegistrationCurrentPlain() || state.DeadAt is not null || state.Hitpoints == 0) return false;
                var captured = new OwnedDragonhowlSobTarget(owner, catalog, birth, state);
                if (!captured.BeforeCurrentPlain()) return false;
                current = Visible(birth, captured.before);
                next = captured;
                return true;
            }
        }

        // DATA projection for prebuilding exact source output. It issues no
        // permission and changes neither the target nor the proc purpose.
        internal bool TryProject(uint damage, LegacySobNpcDeathBehavior behavior,
            out SobNpc visibleAfter, out SobNpcDamageResult result)
        {
            visibleAfter = null!; result = default;
            lock (owner._gate)
            {
                if (invoked || !BeforeCurrentPlain() || !TryProjectImage(damage, default, behavior, out var after, out result)) return false;
                visibleAfter = Visible(birth, after); return true;
            }
        }

        internal bool CanPrepareEventReset(uint damage, LegacySobNpcDeathBehavior behavior,
            WorldPacketHandlers.PreparedDragonhowlSobEvent plan)
        {
            lock (owner._gate)
            {
                if (invoked || plan.ExpectedResetSob is null || !BeforeCurrentPlain() ||
                    !TryProjectImage(damage, default, behavior, out var projected, out var result) || !result.Killed) return false;
                var reset = projected with
                {
                    Version = checked(projected.Version + 1), Hitpoints = projected.Maximum,
                    DeadAt = null, Name = plan.ResetName, Guild = plan.ResetGuildId ?? projected.Guild,
                    GateNotified = false,
                };
                return Visible(birth, reset) == plan.ExpectedResetSob;
            }
        }

        internal bool TryCommit(WorldSessionStore.OwnedDragonhowlNonPlayer purpose, out SobNpcDamageResult result)
        {
            result = default;
            // Store/lifecycle/actor proof comes before the SOB gate. Never
            // reverse the real lock order by acquiring a Store scope here.
            if (!purpose.TryReadSobBefore(this, out var expected, out var damage, out var now, out var behavior)) return false;
            lock (owner._gate)
            {
                if (invoked || !BeforeCurrentPlain() || Visible(birth, before) != expected ||
                    !TryProjectImage(damage, now, behavior, out var after, out var projected)) return false;
                var receipt = new CommittedDamage(after, projected);
                invoked = true;
                result = owner.ApplyDamage(birth, damage, now, behavior);
                if (result != projected || !RegistrationCurrentPlain() || SobStateImage.Capture(state) != after)
                    throw new InvalidOperationException("ownedDragonhowlSobCommittedOutcomeChanged");
                committed = receipt;
                return true;
            }
        }

        internal bool IsCommittedCurrent()
        {
            lock (owner._gate)
                return committed is { } actual && RegistrationCurrentPlain() && SobStateImage.Capture(state) == actual.After;
        }

        internal bool TryCommitEventReset(WorldSessionStore.OwnedDragonhowlNonPlayer purpose,
            WorldPacketHandlers.PreparedDragonhowlSobEvent plan)
        {
            // The private Store stage identifies this exact registered target
            // and event after damage/score. It is denial-only if any part fails.
            if (!plan.IsPreparedFor(purpose) || plan.ExpectedResetSob is null ||
                !purpose.CoversSobEventReset(this, plan)) return false;
            lock (owner._gate)
            {
                if (eventReset || committed is not { Result.Killed: true } actual ||
                    !RegistrationCurrentPlain() || SobStateImage.Capture(state) != actual.After) return false;
                var after = actual.After with
                {
                    Version = checked(actual.After.Version + 1), Hitpoints = actual.After.Maximum,
                    DeadAt = null, Name = plan.ResetName, Guild = plan.ResetGuildId ?? actual.After.Guild,
                    GateNotified = false,
                };
                if (Visible(birth, after) != plan.ExpectedResetSob) return false;
                var receipt = new CommittedDamage(after, actual.Result);
                eventReset = true;
                owner.ResetHitpointsToMax(birth, plan.ResetName, plan.ResetGuildId);
                if (!RegistrationCurrentPlain() || SobStateImage.Capture(state) != after)
                    throw new InvalidOperationException("ownedDragonhowlSobEventResetChanged");
                committed = receipt;
                return true;
            }
        }

        internal bool TryReadCommitted(out SobNpc current, out SobNpcDamageResult result)
        {
            current = null!; result = default;
            lock (owner._gate)
            {
                if (committed is not { } actual || !RegistrationCurrentPlain() || SobStateImage.Capture(state) != actual.After) return false;
                current = Visible(birth, actual.After); result = actual.Result; return true;
            }
        }

        private bool BeforeCurrentPlain() => RegistrationCurrentPlain() && SobStateImage.Capture(state) == before &&
            state.DeadAt is null && state.Hitpoints > 0;

        private bool RegistrationCurrentPlain() => Monitor.IsEntered(owner._gate) && ReferenceEquals(catalog.SobNpcs, adoptedRows) &&
            IsPlain(adoptedRows) && adoptedRows.Count(row => ReferenceEquals(row, birth)) == 1 &&
            adoptedRows.Count(row => row.Id == birth.Id && row.MapId == birth.MapId) == 1 &&
            owner._states.TryGetValue(SobNpcRuntimeKey.From(birth), out var actual) && ReferenceEquals(actual, state) &&
            ReferenceEquals(state.Birth, birth) && owner._states.Count(row => row.Key.Id == birth.Id && row.Key.MapId == birth.MapId) == 1 &&
            IsPlain(birth.Fields) && birth.Fields.SequenceEqual(fields, StringComparer.Ordinal);

        private bool TryProjectImage(uint requested, DateTimeOffset now, LegacySobNpcDeathBehavior behavior,
            out SobStateImage after, out SobNpcDamageResult result)
        {
            after = before; result = default;
            if (behavior is not (LegacySobNpcDeathBehavior.ResetHitpointsAndBroadcastDeathUpdates or
                LegacySobNpcDeathBehavior.GateDemolition or LegacySobNpcDeathBehavior.PersistentDeath)) return false;
            var applied = Math.Min(requested, before.Hitpoints);
            bool killed = applied != 0 && applied == before.Hitpoints;
            if (requested != 0)
            {
                after = before with { Version = checked(before.Version + 1), Hitpoints = before.Hitpoints - applied };
                if (killed)
                {
                    int? mesh = behavior == LegacySobNpcDeathBehavior.GateDemolition
                        ? before.DemolitionMesh ?? LegacySobNpcEventRules.GetDefaultDemolitionMesh(birth) : null;
                    after = behavior switch
                    {
                        LegacySobNpcDeathBehavior.ResetHitpointsAndBroadcastDeathUpdates => after with { Hitpoints = before.Maximum },
                        LegacySobNpcDeathBehavior.GateDemolition => after with { Hitpoints = before.Maximum, Mesh = mesh },
                        _ => after with { DeadAt = now, Mesh = mesh },
                    };
                }
            }
            result = new(applied, after.Hitpoints, killed, AlreadyDead: false);
            return true;
        }

        private static SobNpc Visible(SobNpc birth, SobStateImage image) => birth with
        {
            Name = image.Name ?? birth.Name, Life = ToSeedHitpoints(image.Hitpoints),
            MaxLife = ToSeedHitpoints(image.Maximum), Lookface = image.Mesh ?? birth.Lookface,
        };
        private static bool IsPlain<T>(IReadOnlyList<T>? source) => source is T[] || source?.GetType() == typeof(List<T>);
        private sealed record CommittedDamage(SobStateImage After, SobNpcDamageResult Result);
        private readonly record struct SobStateImage(ulong Version, uint Hitpoints, uint SeedMaximum, uint Maximum,
            DateTimeOffset? DeadAt, int? Mesh, string? Name, uint Guild, bool GateNotified, int? DemolitionMesh)
        {
            internal static SobStateImage Capture(SobNpcRuntimeState state) => new(state.MutationVersion, state.Hitpoints,
                state.SeedMaxHitpoints, state.MaxHitpoints, state.DeadAt, state.MeshOverride, state.NameOverride,
                state.OwnerGuildId, state.GuildWarGateDestroyedNotified, state.GuildWarGateDemolitionMeshOverride);
        }
    }
}
