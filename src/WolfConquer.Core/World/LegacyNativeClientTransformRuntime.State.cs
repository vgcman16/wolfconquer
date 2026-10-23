namespace WolfConquer.Core.World;

// Native ClientTransform8..25 only: complete physical state and constructor.
// CheckUp/FinishTransform/UpdateStatus/CreateTransform remain whole held methods;
// this state owner does not simulate their HP/equipment/view side effects.
internal partial class LegacyNativeClientTransformRuntime
{
    public uint HitPoints = 0u;
    public ushort ID = 0;
    public DateTime Stamp = default(DateTime);
    public ushort SpellID = 0;
    public ushort Level = 0;
#pragma warning disable CS0414 // Native storage is read by the whole held lifecycle methods.
    private LegacyNativePlayerRuntime Owner;
#pragma warning restore CS0414

    public LegacyNativeClientTransformRuntime(LegacyNativePlayerRuntime owner)
    {
        Owner = owner;
    }
}
