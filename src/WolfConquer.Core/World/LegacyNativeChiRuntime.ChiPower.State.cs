using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeChiRuntime
{
	public partial class ChiPower
	{
		public static readonly LegacyNativeRandomLite Rand = new LegacyNativeRandomLite();

		public uint Exp;

		public ChiAttribute[] Attributes;

		public LegacyNativeMsgChiInfo.ChiPowerType Type;

		public string Name = "";

		public uint UID;

		public int Rank = 0;

		public LegacyNativeFateExpTable.Value? ChiAttributes => LegacyNativeFateExpTable.Values.Values.Where((LegacyNativeFateExpTable.Value i) => i.ChiType == Type && Exp >= i.RequiredPoints).LastOrDefault();

		// Each ushort-valued term is finite and within Int64 range. Preserve the
		// Framework/x86 conv.u4 low32-bit truncation for under-minimum attributes
		// (parent-owned .artifacts/chi-native-conversion-probe,17/17 controls);
		// direct .NET9 double-to-uint conversion saturates those negatives instead.
		// The uint selector binds to Enumerable.Sum's Int64 overload; preserve
		// that widening and the final native Int32 truncation after the sum.
		public int Score => (int)(UnLocked ? Attributes.Sum((ChiAttribute x) => unchecked((uint)(long)((double)(x.Value - ChiMinValues(x.Type)) / ((double)(ChiMaxValues(x.Type) - ChiMinValues(x.Type)) / 100.0)))) : 0);

		public bool UnLocked { get; set; }

		public ChiPower(LegacyNativeMsgChiInfo.ChiPowerType _Type = LegacyNativeMsgChiInfo.ChiPowerType.None)
		{
			Attributes = new ChiAttribute[4];
			Type = _Type;
		}
	}
}
