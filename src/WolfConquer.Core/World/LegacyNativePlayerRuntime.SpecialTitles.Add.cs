using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    internal void AddSpecialTitle(LegacyNativeMsgTitleStorage.TitleType type, LegacyNativePacket stream)
    {
        if (LegacyNativeTitleStorage.Titles.TryGetValue((uint)type, out var value) && !SpecialTitles.Contains(type))
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
            SpecialTitles.Add(type);
        }
    }
}
