using System.Runtime.InteropServices;
namespace WolfConquer.Core.World;

// Whole native metadata; bracket packet receiver/lifecycle is not registered here.
[StructLayout(LayoutKind.Explicit)]
internal partial struct LegacyNativeMsgElitePKBrackets
{
public enum GuiTyp : ushort
	{
		GUI_Top8Ranking = 0,
		GUI_Knockout = 3,
		GUI_Top8Qualifier = 4,
		GUI_Top4Qualifier = 5,
		GUI_Top2Qualifier = 6,
		GUI_Top3 = 7,
		GUI_Top1 = 8,
		GUI_ReconstructTop = 9
	}
public enum Action : ushort
	{
		InitialList,
		StaticUpdate,
		GUIEdit,
		UpdateList,
		RequestInformation,
		StopWagers,
		EPK_State
	}
}
