using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete source family; full lifecycle/interface adoption is joint, never a no-op.
internal partial class LegacyNativeKillerSystem
{
	public enum KillFlag : byte
	{
		None,
		FirstBloodKill,
		DoubleKill,
		TripleKill,
		UnStoppable
	}

	public KillFlag Flag = KillFlag.None;
	public bool Dead = false;
	public uint UID = 0u;

	public static string GetSound(LegacyNativeKillerSystem user)
	{
		return user.Flag switch
		{
			KillFlag.FirstBloodKill => "sound/fc1.wav",
			KillFlag.DoubleKill => "sound/fc2.wav",
			KillFlag.TripleKill => "sound/fc3.wav",
			KillFlag.UnStoppable => "sound/fc4.wav",
			_ => "none",
		};
	}

	public void CheckDead(uint _UID)
	{
		if (_UID == UID)
		{
			Dead = true;
		}
	}
}
