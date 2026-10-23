using System;
using WolfConquer.Core.Networking;
using ProtoBuf;

namespace WolfConquer.Core.World;

internal static class LegacyNativeMsgRefineEffect
{
	[ProtoContract]
	public class RefineEffectProto
	{
		[ProtoMember(1, IsRequired = true)]
		public uint Id;

		[ProtoMember(2, IsRequired = true)]
		public uint dwParam;

		[ProtoMember(3, IsRequired = true)]
		public RefineEffects Effect;
	}

	[Flags]
	public enum RefineEffects : byte
	{
		None = 0,
		ToxinEraserLevel = 1,
		StrikeLockLevel = 2,
		LuckyStrike = 3,
		CalmWind = 4,
		DrainingTouch = 5,
		BloodSpawn = 6,
		LightOfStamina = 7,
		ShiledBreak = 8,
		KillingFlash = 9,
		MirrorOfSin = 0xA,
		DivineGuard = 0xB,
		CoreStrike = 0xC,
		InvisbleArrow = 0xD,
		FreeSoul = 0xE,
		StraightLife = 0xF,
		AbsoluteLuck = 0x10
	}

	public static LegacyNativePacket MsgRefineEffectCreate(this LegacyNativePacket stream, RefineEffectProto obj)
	{
		stream.InitWriter();
		stream.ProtoBufferSerialize(obj);
		stream.Finalize(2319);
		return stream;
	}

    internal static byte[] PrepareRefinementPayload(uint uid, RefineEffects effect)
    {
        using var serialized = new System.IO.MemoryStream();
        Serializer.Serialize(serialized, new RefineEffectProto { Id = uid, dwParam = uid, Effect = effect });
        return serialized.ToArray();
    }
}
