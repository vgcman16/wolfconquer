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

    public uint DynamicID { get; set; }

    public uint Map
    {
        get
        {
            return _mmmap;
        }
        set
        {
            _mmmap = value;
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
            Px = X;
            xx = value;
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
            Py = Y;
            yy = value;
        }
    }
}
