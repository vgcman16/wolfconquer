#nullable disable

namespace WolfConquer.Core.Networking;

public class LegacyNativeTQCast5
{
	private byte[] EncIvec;

	private byte[] DecIvec;

	private int[] EncNum;

	private int[] DecNum;

	private LegacyNativeTQCast5Impl Impl;

	public LegacyNativeTQCast5()
	{
		Impl = new LegacyNativeTQCast5Impl();
		Reset();
	}

	public void GenerateKey(byte[] kb)
	{
		byte[] array = new byte[16];
		for (int i = 0; i < 16; i++)
		{
			array[i] = kb[i];
		}
		Impl.SetKey(array);
	}

	public void Reset()
	{
		EncIvec = new byte[16];
		DecIvec = new byte[16];
		DecNum = new int[8];
		EncNum = new int[8];
	}

	public unsafe void Encrypt(byte* buffer_in, int in_off, byte[] buffer_out, int out_off, int length)
	{
		int num = length;
		int num2 = EncNum[0];
		int num3 = in_off;
		int num4 = 0;
		while (num > 0)
		{
			if (num2 == 0)
			{
				Impl.EncryptBlock(EncIvec, 0, EncIvec, 0);
			}
			byte b = (byte)((buffer_in[num3++] ^ EncIvec[num2]) & 0xFF);
			buffer_out[num4++] = b;
			EncIvec[num2] = b;
			num2 = (num2 + 1) & 7;
			EncNum[0] = num2;
			num--;
		}
	}

	public unsafe void Decrypt(byte[] buffer_in, int in_off, byte* buffer_out, int out_off, int length)
	{
		int num = length;
		int num2 = DecNum[0];
		int num3 = in_off;
		int num4 = 0;
		while (num > 0)
		{
			if (num2 == 0)
			{
				Impl.EncryptBlock(DecIvec, 0, DecIvec, 0);
			}
			byte b = buffer_in[num3++];
			byte b2 = DecIvec[num2];
			DecIvec[num2] = b;
			buffer_out[out_off + num4] = (byte)((b2 ^ b) & 0xFF);
			num4++;
			num2 = (num2 + 1) & 7;
			DecNum[0] = num2;
			num--;
		}
	}
}
