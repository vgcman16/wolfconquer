using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Core.World;

// Complete native MrRayzo.Role.Instance/Activeness.cs; no host birth, startup, save caller or SQL route installed.
internal class LegacyNativeActivenessRuntime
{
	public class Task
	{
		public uint ID;

		public byte Progres;

		public byte Completed;
	}

	public LegacyNativeGameClientRuntime user;

	public byte ClaimRewards = 0;

	public LegacyNativeDbSafeDictionary<uint, Task> Tasks;

	public uint ActivityPoints = 0u;

	public void UpdateActivityPoints(LegacyNativePacket stream)
	{
		user.Send(stream.ActivityRewardsInfoCreate(LegacyNativeMsgActivityRewardsInfo.Action.InitializeList, ActivityPoints));
	}

	public LegacyNativeActivenessRuntime(LegacyNativeGameClientRuntime client)
	{
		user = client;
		Tasks = new LegacyNativeDbSafeDictionary<uint, Task>();
	}

	public void IncreaseTask(uint UID)
	{
		if (LegacyNativePoolSaveRecords.ActivityTasks.TryGetValue(UID, out var value))
		{
			if (Tasks.ContainsKey(UID))
			{
				UpdateTask(UID, value);
			}
			else if (value.CheckOpen(user.Player.Level, user.Player.Reborn))
			{
				Tasks.Add(UID, new Task
				{
					ID = UID
				});
				UpdateTask(UID, value);
			}
		}
	}

	private void UpdateTask(uint ID, LegacyNativeActivityTaskTable.Task DBTask)
	{
		Task task = Tasks[ID];
		if (task.Completed != 0)
		{
			return;
		}
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		if (task.Progres < DBTask.NeedProgress)
		{
			task.Progres++;
		}
		if (task.Progres == DBTask.NeedProgress)
		{
			task.Completed = 1;
			ActivityPoints += DBTask.ActiveValue;
			UpdateActivityPoints(stream);
		}
		UpdateSingleTask(task, stream);
	}

	public void UpdateSingleTask(Task task, LegacyNativePacket stream)
	{
		user.Send(stream.ActivityTasksCreate(LegacyNativeMsgActivityTasks.Action.InitializeList, new Task[1] { task }));
	}

	public void CheckTasks(LegacyNativePacket stream)
	{
		LegacyNativeActivityTaskTable.Task[] values = LegacyNativePoolSaveRecords.ActivityTasks.GetValues();
		foreach (LegacyNativeActivityTaskTable.Task task in values)
		{
			if (task.CheckOpen(user.Player.Level, user.Player.Reborn) && !Tasks.ContainsKey(task.ID))
			{
				Tasks.Add(task.ID, new Task
				{
					ID = task.ID
				});
			}
		}
		UpdateTasksList(stream);
	}

	public void UpdateTasksList(LegacyNativePacket stream)
	{
		user.Send(stream.ActivityTasksCreate(LegacyNativeMsgActivityTasks.Action.InitializeList, Tasks.GetValues()));
	}

	public void UpdateClaimRewards(LegacyNativePacket stream)
	{
		user.Send(stream.ActivityClaimsCreate(ClaimRewards));
	}

	public override string ToString()
	{
		LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
		writeLine.Add(ClaimRewards);
		writeLine.Add(ActivityPoints);
		writeLine.Add(Tasks.Count);
		Task[] values = Tasks.GetValues();
		foreach (Task task in values)
		{
			writeLine.Add(task.ID).Add(task.Progres).Add(task.Completed);
		}
		return writeLine.Close();
	}

	public void Load(string Line)
	{
		LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(Line, '/');
		ClaimRewards = readLine.Read((byte)0);
		ActivityPoints = readLine.Read((byte)0);
		int num = readLine.Read(0);
		for (int i = 0; i < num; i++)
		{
			Task task = new Task();
			task.ID = readLine.Read(0u);
			task.Progres = readLine.Read((byte)0);
			task.Completed = readLine.Read((byte)0);
			if (!Tasks.ContainsKey(task.ID))
			{
				Tasks.Add(task.ID, task);
			}
		}
	}
}
