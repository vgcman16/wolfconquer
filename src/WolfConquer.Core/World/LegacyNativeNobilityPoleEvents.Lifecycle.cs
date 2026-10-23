using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgNobilityPole
{
    public static void End()
    {
        if (Proces == LegacyNativeProcesType.Alive)
        {
            Proces = LegacyNativeProcesType.Dead;
            LegacyNativeMsgSchedules.SendSysMesage((RoundOwner == null) ? "NobilityPoleWar has ended and there is no winner" : "NobilityPoleWar  has ended , Go Claim ur Prize <3.", LegacyNativeMsgMessage.ChatMode.Center);
            NobilityPoleList.Clear();
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole1
{
    public static void Start()
    {
        RoundOwner = null;
        NobilityPoleList = new LegacyNativeDbSafeDictionary<uint, Info>();
        Proces = LegacyNativeProcesType.Alive;
    }

    public static void End()
    {
        if (Proces == LegacyNativeProcesType.Alive)
        {
            Proces = LegacyNativeProcesType.Dead;
            NobilityPoleList.Clear();
        }
    }

    public static void CheckUP()
    {
        DateTime now = DateTime.Now;
        if (Proces == LegacyNativeProcesType.Dead && now.Hour == 8 && now.Minute == 0 && now.Second <= 1)
        {
            Start();
        }
        if (Proces == LegacyNativeProcesType.Alive && now.Hour == 8 && now.Minute == 30 && now.Second > 1)
        {
            End();
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole2
{
    public static void Start()
    {
        RoundOwner = null;
        NobilityPoleList = new LegacyNativeDbSafeDictionary<uint, Info>();
        Proces = LegacyNativeProcesType.Alive;
    }

    public static void End()
    {
        if (Proces == LegacyNativeProcesType.Alive)
        {
            Proces = LegacyNativeProcesType.Dead;
            NobilityPoleList.Clear();
        }
    }

    public static void CheckUP()
    {
        DateTime now = DateTime.Now;
        if (Proces == LegacyNativeProcesType.Dead && now.Hour == 8 && now.Minute == 0 && now.Second <= 1)
        {
            Start();
        }
        if (Proces == LegacyNativeProcesType.Alive && now.Hour == 8 && now.Minute == 30 && now.Second > 1)
        {
            End();
        }
    }
}

internal partial class LegacyNativeMsgNobilityPole3
{
    public static void Start()
    {
        RoundOwner = null;
        NobilityPoleList = new LegacyNativeDbSafeDictionary<uint, Info>();
        Proces = LegacyNativeProcesType.Alive;
    }

    public static void End()
    {
        if (Proces == LegacyNativeProcesType.Alive)
        {
            Proces = LegacyNativeProcesType.Dead;
            NobilityPoleList.Clear();
        }
    }

    public static void CheckUP()
    {
        DateTime now = DateTime.Now;
        if (Proces == LegacyNativeProcesType.Dead && now.Hour == 8 && now.Minute == 0 && now.Second <= 1)
        {
            Start();
        }
        if (Proces == LegacyNativeProcesType.Alive && now.Hour == 8 && now.Minute == 30 && now.Second > 1)
        {
            End();
        }
    }
}
