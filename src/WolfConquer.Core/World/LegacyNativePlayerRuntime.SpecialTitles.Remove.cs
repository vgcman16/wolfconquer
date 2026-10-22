using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    internal void RemoveSpecialTitle(LegacyNativeMsgTitleStorage.TitleType type, LegacyNativePacket stream)
    {
        if (LegacyNativeTitleStorage.Titles.TryGetValue((uint)type, out var value))
        {
            if (SpecialTitles.Contains(type))
            {
                SpecialTitles.Remove(type);
            }
            LegacyNativeMsgTitleStorage.TitleStorage titleStorage = new LegacyNativeMsgTitleStorage.TitleStorage();
            titleStorage.ActionID = LegacyNativeMsgTitleStorage.Action.UnEquip;
            titleStorage.dwparam2 = value.ID;
            titleStorage.dwparam3 = value.SubID;
            Owner.Send(stream.CreateTitleStorage(titleStorage));
            titleStorage.dwparam1 = GetTitlesScore();
            titleStorage.Title = new LegacyNativeMsgTitleStorage.Title();
            titleStorage.Title.ID = value.ID;
            titleStorage.Title.SubId = value.SubID;
            titleStorage.Title.dwparam1 = 0u;
            Owner.Send(stream.CreateTitleStorage(titleStorage));
            titleStorage.ActionID = LegacyNativeMsgTitleStorage.Action.UpdateScore;
            Owner.Send(stream.CreateTitleStorage(titleStorage));
            titleStorage.ActionID = LegacyNativeMsgTitleStorage.Action.RemoveTitle;
            Owner.Send(stream.CreateTitleStorage(titleStorage));
            if (SpecialTitleID / 10000 == (uint)type)
            {
                SpecialTitleID = (SpecialTitleScore = 0u);
            }
            else if (SpecialWingID / 10000 == (uint)type)
            {
                SpecialWingID = 0u;
            }
        }
    }
}
