using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeChiRuntime
{
	public partial class ChiPower
	{
		public override string ToString()
		{
			LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/').Add(UnLocked ? 1 : 0).Add(Exp);
			for (int i = 0; i < Attributes.Length; i++)
			{
				writeLine.Add((byte)Attributes[i].Type);
				writeLine.Add(Attributes[i].Value);
				writeLine.Add((byte)(Attributes[i].Locked ? 1u : 0u));
			}
			return writeLine.Close();
		}

		public void Load(string line, uint _UID, string _Name)
		{
			UID = _UID;
			Name = _Name;
			LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(line, '/');
			UnLocked = readLine.Read((byte)0) == 1;
			if (UnLocked)
			{
				Exp = readLine.Read(0u);
				for (int i = 0; i < Attributes.Length; i++)
				{
					Attributes[i].Type = (LegacyNativeChiAttribute)readLine.Read((byte)0);
					Attributes[i].Value = readLine.Read((ushort)0);
					Attributes[i].Locked = readLine.Read((byte)0) > 0;
				}
			}
		}
	}
}
