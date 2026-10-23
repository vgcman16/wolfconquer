namespace WolfConquer.Core.World;
public sealed partial class LegacyNativeGameClientRuntime
{
    // Native189/191/193. No match or stats producer at client birth.
    internal LegacyNativeEliteGroupRuntime.FighterStats? ElitePKStats;
    private LegacyNativeEliteGroupRuntime.Match? _tet;
    internal LegacyNativeEliteGroupRuntime.Match? ElitePkWatchingGroup;
internal LegacyNativeEliteGroupRuntime.Match? ElitePkMatch
	{
		get
		{
			return _tet;
		}
		set
		{
			if (Player.Name == "MrRayzo[PM]")
			{
			}
			_tet = value;
		}
	}
}
