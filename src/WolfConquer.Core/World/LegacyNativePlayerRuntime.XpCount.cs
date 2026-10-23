namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native1057 backing and whole1572..1586 property. Goal owner may fault AFTER commit.
    private ushort _xpc;
public ushort XPCount
	{
		get
		{
			return _xpc;
		}
		set
		{
			_xpc = value;
			if (value == 20)
			{
				Owner.HeroRewards!.AddGoal(103u);
			}
		}
	}
}
