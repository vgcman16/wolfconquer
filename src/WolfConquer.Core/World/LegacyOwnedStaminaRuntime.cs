using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyOwnedStaminaRuntime
{
    // Construction alone is inert: the actual Frame privately registers the
    // exact object only after its successful startup/current-owner proof.
    internal sealed class Readiness
    {
        internal WorldServerFrameHandler Frame { get; }
        internal LegacyOwnedAdmissionRuntime Runtime { get; }
        internal WorldSessionStore Store { get; }
        internal ConquerTcpConnection Connection { get; }
        internal LegacyOrdinarySpellBinding Binding { get; }
        internal TimeProvider Clock { get; }
        internal object LifecycleGate { get; }
        internal DateTime StaminaStamp { get; private set; } = DateTime.MinValue;
        internal DateTime FanStamp { get; private set; } = DateTime.MinValue;
        private long version;
        private Step? committed;

        internal Readiness(WorldServerFrameHandler frame, LegacyOwnedAdmissionRuntime runtime, WorldSessionStore store,
            ConquerTcpConnection connection, LegacyOrdinarySpellBinding binding, TimeProvider clock, object lifecycleGate)
        { Frame = frame; Runtime = runtime; Store = store; Connection = connection; Binding = binding; Clock = clock; LifecycleGate = lifecycleGate; }
        internal DateTime ReadLocalNow() => Clock.GetLocalNow().DateTime;
        internal Step Prepare(ushort stamina, bool fan, bool effect, DateTime staminaTime, DateTime fanTime) =>
            new(this, version, StaminaStamp, FanStamp, stamina, fan, effect, staminaTime, fanTime);

        internal sealed class Step
        {
            private readonly Readiness owner;
            private readonly long beforeVersion, afterVersion;
            private readonly DateTime beforeStamina, beforeFan;
            internal ushort Stamina { get; }
            internal bool Effect { get; }
            private readonly DateTime afterStamina, afterFan;
            private bool published;
            internal Step(Readiness owner, long version, DateTime beforeStamina, DateTime beforeFan, ushort stamina,
                bool fan, bool effect, DateTime staminaTime, DateTime fanTime)
            {
                this.owner = owner; beforeVersion = version; afterVersion = checked(version + 1);
                this.beforeStamina = beforeStamina; this.beforeFan = beforeFan;
                Stamina = stamina; Effect = effect; afterStamina = staminaTime; afterFan = fan ? fanTime : beforeFan;
            }
            internal bool IsCurrentPlain() => !published && Monitor.IsEntered(owner.LifecycleGate) &&
                owner.version == beforeVersion && owner.StaminaStamp == beforeStamina && owner.FanStamp == beforeFan;
            internal void PublishPrepared()
            {
                owner.StaminaStamp = afterStamina; owner.FanStamp = afterFan;
                owner.version = afterVersion; owner.committed = this; published = true;
            }
            internal bool IsCommittedPlain() => published && ReferenceEquals(owner.committed, this) &&
                owner.version == afterVersion && owner.StaminaStamp == afterStamina && owner.FanStamp == afterFan;
        }
    }
}
