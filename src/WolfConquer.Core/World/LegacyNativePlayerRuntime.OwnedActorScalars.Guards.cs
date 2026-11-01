namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private LegacyOrdinarySpellBinding? ownedActorScalarsBinding;
    internal bool HasOwnedActorScalarsAttachment => Volatile.Read(ref ownedActorScalarsBinding) is not null ||
        Owner.OrdinarySpellBinding is not null || Owner.MySpells?.OrdinaryBinding is not null;

    // Whole legacy executors call this before any earlier stat/cursor effect.
    internal void RequireUnboundActorScalarMutation()
    {
        if (HasOwnedActorScalarsAttachment) throw new InvalidOperationException("ordinaryActorScalarsRequireOwnedStorePlan");
        if (Monitor.IsEntered(SpellAdmissionSyncRoot) && !Monitor.IsEntered(Owner.SpellAdmissionSyncRoot))
            throw new InvalidOperationException("nativeActorScalarActorOrderInvalid");
    }

    private enum ActorScalar { Reborn, Agility, X, Y, Map, DynamicID }

    private void SetUnboundActorScalar(ActorScalar field, uint value)
    {
        RequireUnboundActorScalarMutation();
        lock (Owner.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            RequireUnboundActorScalarMutation();
            // Each public property's CLR type already supplies its exact width.
            // Unbound X/Y retain their individual source cursor side effects.
            switch (field)
            {
                case ActorScalar.Reborn: _reborn = (byte)value; break;
                case ActorScalar.Agility: _agility = (ushort)value; break;
                case ActorScalar.X: Px = xx; xx = (ushort)value; break;
                case ActorScalar.Y: Py = yy; yy = (ushort)value; break;
                case ActorScalar.Map: _mmmap = value; break;
                case ActorScalar.DynamicID: _dynamicId = value; break;
            }
        }
    }
}
