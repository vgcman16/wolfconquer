using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeMsgWeather
{
	public enum WeatherType : uint
	{
		Nothing = 1u,
		Rain = 2u,
		Snow = 3u,
		RainWind = 4u,
		AutumnLeaves = 5u,
		CherryBlossomPetals = 7u,
		CherryBlossomPetalsWind = 8u,
		BlowingCotten = 9u,
		Atoms = 10u
	}

	public static LegacyNativePacket WeatherCreate(this LegacyNativePacket stream, WeatherType Type, uint nIntensity, uint nDir, uint nColor, uint nSpeedSecs)
	{
		stream.InitWriter();
		stream.Write((uint)Type);
		stream.Write(nIntensity);
		stream.Write(nDir);
		stream.Write(nColor);
		stream.Write(nSpeedSecs);
		stream.Finalize(2306);
		return stream;
	}
}
