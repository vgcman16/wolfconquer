using ProcesType = WolfConquer.Core.World.LegacyNativeProcesType;
using MsgSchedules = WolfConquer.Core.World.LegacyNativeMsgSchedules;
using MsgMessage = WolfConquer.Core.World.LegacyNativeMsgMessage;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgPowerArena
{
	public void Finish()
	{
		if (Process == ProcesType.Alive)
		{
			MsgSchedules.SendSysMesage("The Power Arena is closed, please come tomorrow!", MsgMessage.ChatMode.Talk, MsgMessage.MsgColor.yellow);
			Process = ProcesType.Dead;
		}
	}

	public void CheckUp()
	{
		if (Process == ProcesType.Idle && DateTime.Now > PrepareStart.AddSeconds(60.0))
		{
			if (StartMinutes == 0)
			{
				MsgSchedules.SendSysMesage("The Power Arena is open! Find Arena Manager Wang in Twin City (465,234) to sign up for the Arena.", MsgMessage.ChatMode.Talk, MsgMessage.MsgColor.white);
				Process = ProcesType.Alive;
				StartTimer = DateTime.Now;
				StartMinutes = 5;
			}
			else
			{
				MsgSchedules.SendSysMesage("Power Arena will be opened in " + StartMinutes + " minutes. Please get ready for that!", MsgMessage.ChatMode.Talk, MsgMessage.MsgColor.white);
				PrepareStart = DateTime.Now;
				StartMinutes--;
			}
		}
		if (Process == ProcesType.Alive)
		{
			DateTime now = DateTime.Now;
			if (now > StartTimer.AddMinutes(60.0))
			{
				Finish();
			}
			if (now > StartTimer.AddMinutes(55.0) && DateTime.Now > PrepareStart.AddSeconds(60.0))
			{
				MsgSchedules.SendSysMesage("Power Arena will be closed in " + StartMinutes + " minutes. Go and claim your reward now!", MsgMessage.ChatMode.Talk, MsgMessage.MsgColor.white);
				PrepareStart = DateTime.Now;
				StartMinutes--;
			}
		}
	}
}
