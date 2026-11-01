namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player1079 and scalar source members required by RoleView.CanSee.
    // This does not implement IMapObj or fabricate its missing GetArray serializer.
    internal LegacyNativeRoleView View;

    private uint _mmmap;

    private ushort xx;

    private ushort yy;

    public ushort Px;

    public ushort Py;

    public bool AllowDynamic { get; set; }

    internal LegacyNativeMapObjectType ObjType { get; set; }

    private uint _dynamicId;
    public uint DynamicID
    {
        get => _dynamicId;
        set => SetUnboundActorScalar(ActorScalar.DynamicID, value);
    }

    public uint Map
    {
        get
        {
            return _mmmap;
        }
        set
        {
            SetUnboundActorScalar(ActorScalar.Map, value);
        }
    }

    public ushort X
    {
        get
        {
            return xx;
        }
        set
        {
            SetUnboundActorScalar(ActorScalar.X, value);
        }
    }

    public ushort Y
    {
        get
        {
            return yy;
        }
        set
        {
            SetUnboundActorScalar(ActorScalar.Y, value);
        }
    }
}
