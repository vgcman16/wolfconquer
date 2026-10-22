using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    internal void SendSpecialTitle(LegacyNativePacket stream)
    {
        foreach (LegacyNativeMsgTitleStorage.TitleType specialTitle in SpecialTitles)
        {
            if (LegacyNativeTitleStorage.Titles.TryGetValue((uint)specialTitle, out var value))
            {
                LegacyNativeMsgTitleStorage.TitleStorage titleStorage = new LegacyNativeMsgTitleStorage.TitleStorage();
                titleStorage.ActionID = LegacyNativeMsgTitleStorage.Action.UpdateScore;
                titleStorage.dwparam1 = GetTitlesScore();
                titleStorage.dwparam2 = value.ID;
                titleStorage.dwparam3 = value.SubID;
                titleStorage.Title = new LegacyNativeMsgTitleStorage.Title();
                titleStorage.Title.ID = value.ID;
                titleStorage.Title.SubId = value.SubID;
                Owner.Send(stream.CreateTitleStorage(titleStorage));
                titleStorage.dwparam1 = 100u;
                titleStorage.ActionID = LegacyNativeMsgTitleStorage.Action.FullLoad;
                Owner.Send(stream.CreateTitleStorage(titleStorage));
            }
        }
    }

    internal void ActiveSpecialTitles(LegacyNativePacket stream)
    {
        if (LegacyNativeTitleStorage.Titles.TryGetValue(SpecialTitleID / 10000, out var value))
        {
            LegacyNativeMsgTitleStorage.TitleStorage titleStorage = new LegacyNativeMsgTitleStorage.TitleStorage();
            titleStorage.ActionID = LegacyNativeMsgTitleStorage.Action.UpdateScore;
            titleStorage.dwparam1 = GetTitlesScore();
            titleStorage.dwparam2 = value.ID;
            titleStorage.dwparam3 = value.SubID;
            titleStorage.Title = new LegacyNativeMsgTitleStorage.Title();
            titleStorage.Title.ID = value.ID;
            titleStorage.Title.SubId = value.SubID;
            titleStorage.Title.dwparam1 = 100u;
            Owner.Send(stream.CreateTitleStorage(titleStorage));
        }
        if (LegacyNativeTitleStorage.Titles.TryGetValue(SpecialWingID / 10000, out value))
        {
            LegacyNativeMsgTitleStorage.TitleStorage titleStorage2 = new LegacyNativeMsgTitleStorage.TitleStorage();
            titleStorage2.ActionID = LegacyNativeMsgTitleStorage.Action.UpdateScore;
            titleStorage2.dwparam1 = GetTitlesScore();
            titleStorage2.dwparam2 = value.ID;
            titleStorage2.dwparam3 = value.SubID;
            titleStorage2.Title = new LegacyNativeMsgTitleStorage.Title();
            titleStorage2.Title.ID = value.ID;
            titleStorage2.Title.SubId = value.SubID;
            titleStorage2.Title.dwparam1 = 100u;
            Owner.Send(stream.CreateTitleStorage(titleStorage2));
        }
    }
}
