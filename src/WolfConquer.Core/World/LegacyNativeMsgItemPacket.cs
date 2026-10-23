
namespace WolfConquer.Core.World;

internal class LegacyNativeMsgItemPacket
{
	public enum EffectMonsters : uint
	{
		None = 0u,
		EarthquakeLeftRight = 1u,
		EarthquakeUpDown = 2u,
		Night = 4u,
		EarthquakeAndNight = 5u
	}

	public const uint DBShowerEffect = 17u;

	public const uint TwilightDance = 40u;

	public const uint NormalDaggerStorm = 50u;

	public const uint SoulOneDaggerStorm = 41u;

	public const uint SoulTwoDaggerStorm = 42u;

	public const uint InfernalEcho = 1001390u;

	public const uint WrathoftheEmperor = 1001380u;

	public const uint AuroraLotus = 930u;

	public const uint SpaceLeap = 930u;

	public const uint FlameLotus = 940u;

	public const uint RageofWar = 1500u;

	public const uint ShadowofChaser = 1550u;

	public const uint HorrorofStomper = 1530u;

	public const uint PeaceofStomper = 1540u;

	public const uint Thundercloud = 3843u;

	public const uint FireArrow = 3600u;

	public const uint SeaBurial = 1850u;

	public const uint BloomOfDeath = 1860u;

	public const uint TideTrap = 1080u;

	public const uint SickleWind = 2540u;

	public const uint FlameofDestruction = 2530u;

	public const uint WaterShockwave = 2560u;

	public const uint DustDetachment = 2570u;

	public const uint WildFireball = 18456u;

	public uint m_UID;

	public uint m_ID;

	public ushort m_X;

	public ushort m_Y;

	public ushort MaxLife;

	public LegacyNativeMsgDropID DropType;

	public uint Life;

	public byte m_Color;

	public byte m_Color2;

	public uint ItemOwnerUID;

	public byte DontShow;

	public uint GuildID;

	public LegacyNativePlayerMapFlags.ConquerAngle Angle;

	public byte FlowerType;

	public ulong Timer;

	public string? Name;

	public uint UnKnow;

	public byte Plus;

	public uint MapID;

	public ushort OwnerX;

	public ushort OwnerY;

	public static LegacyNativeMsgItemPacket Create()
	{
		return new LegacyNativeMsgItemPacket();
	}

}
