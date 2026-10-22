using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Physical Player and real DbReadLine/DbWriteLine only. No SaveClient/INI,
// character login, quest handler, socket, worker or callback/provider is installed.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeEpicTrojanTests
{
    private const string Sequence = "1/2/3/4/5/6/7/8/9/10/11/12/13/14/15/16/17/18/19/20/";
    private const string DefaultRecord = "0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/";
    private static readonly int[] ByteSlots = [2, 4, 5, 14, 18, 19];
    private static readonly int[] UIntSlots = [0, 1, 3, 6, 7, 8, 9, 10, 11, 12, 13, 15, 16, 17];

    private static LegacyNativePlayerRuntime Player() =>
        new LegacyNativeGameClientRuntime { Fake = true, FullLoading = false }.Player;

    private static LegacyNativePlayerRuntime Seed(int first = 101)
    {
        var player = Player();
        player.EpicTrojanItemChange[0] = (uint)first;
        player.EpicTrojanItemChange[1] = (uint)(first + 1);
        player.StageEpicTrojanQuest = (byte)(first + 2);
        player.ChangeEpicTrojan = (uint)(first + 3);
        player.CanChangeEpicMaterial = (byte)(first + 4);
        player.CanChangeArrayEpicMaterial = (byte)(first + 5);
        player.ChangeArrayEpicTrojan = (uint)(first + 6);
        player.EpicTrojanArrayItemChange[0] = (uint)(first + 7);
        player.EpicTrojanArrayItemChange[1] = (uint)(first + 8);
        player.EpicTrojanEvilArrayPoints = (uint)(first + 9);
        player.EpicTrojanAbysalStage = (uint)(first + 10);
        player.EpicTrojanMr_MirrorItemChange[0] = (uint)(first + 11);
        player.EpicTrojanMr_MirrorItemChange[1] = (uint)(first + 12);
        player.ChangeMr_MirrorEpicTrojan = (uint)(first + 13);
        player.CanChangeMr_MirrorEpicMaterial = (byte)(first + 14);
        player.EpicTrojanGeneralPakItemChange[0] = (uint)(first + 15);
        player.EpicTrojanGeneralPakItemChange[1] = (uint)(first + 16);
        player.ChangeGeneralPakEpicTrojan = (uint)(first + 17);
        player.CanChangGeneralPakMaterial = (byte)(first + 18);
        player.EpicTrojanMrMirrorPrograss = (byte)(first + 19);
        player.EpicTrijanKillGhostReaver = 201;
        return player;
    }

    private static uint[] Values(LegacyNativePlayerRuntime player) =>
    [
        player.EpicTrojanItemChange[0], player.EpicTrojanItemChange[1], player.StageEpicTrojanQuest,
        player.ChangeEpicTrojan, player.CanChangeEpicMaterial, player.CanChangeArrayEpicMaterial,
        player.ChangeArrayEpicTrojan, player.EpicTrojanArrayItemChange[0], player.EpicTrojanArrayItemChange[1],
        player.EpicTrojanEvilArrayPoints, player.EpicTrojanAbysalStage,
        player.EpicTrojanMr_MirrorItemChange[0], player.EpicTrojanMr_MirrorItemChange[1],
        player.ChangeMr_MirrorEpicTrojan, player.CanChangeMr_MirrorEpicMaterial,
        player.EpicTrojanGeneralPakItemChange[0], player.EpicTrojanGeneralPakItemChange[1],
        player.ChangeGeneralPakEpicTrojan, player.CanChangGeneralPakMaterial, player.EpicTrojanMrMirrorPrograss
    ];

    private static uint[] Scalars(LegacyNativePlayerRuntime player) =>
    [
        player.StageEpicTrojanQuest, player.ChangeEpicTrojan, player.CanChangeEpicMaterial,
        player.EpicTrojanEvilArrayPoints, player.EpicTrojanAbysalStage, player.ChangeArrayEpicTrojan,
        player.CanChangeArrayEpicMaterial, player.EpicTrijanKillGhostReaver,
        player.ChangeMr_MirrorEpicTrojan, player.CanChangeMr_MirrorEpicMaterial,
        player.ChangeGeneralPakEpicTrojan, player.CanChangGeneralPakMaterial, player.EpicTrojanMrMirrorPrograss
    ];

    private static uint[]?[] Arrays(LegacyNativePlayerRuntime player) =>
    [
        player.EpicTrojanItemChange, player.EpicTrojanArrayItemChange,
        player.EpicTrojanMr_MirrorItemChange, player.EpicTrojanGeneralPakItemChange
    ];

    private static void SetArray(LegacyNativePlayerRuntime player, int index, uint[]? array)
    {
        switch (index)
        {
            case 0: player.EpicTrojanItemChange = array!; break;
            case 1: player.EpicTrojanArrayItemChange = array!; break;
            case 2: player.EpicTrojanMr_MirrorItemChange = array!; break;
            case 3: player.EpicTrojanGeneralPakItemChange = array!; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private static void SameArrays(LegacyNativePlayerRuntime player, uint[]?[] expected)
    {
        var actual = Arrays(player);
        for (int i = 0; i < 4; i++) Assert.Same(expected[i], actual[i]);
    }

    private static string[] Tokens() => Enumerable.Range(1, 20)
        .Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo old = CultureInfo.CurrentCulture;
        private readonly CultureInfo oldUi = CultureInfo.CurrentUICulture;
        internal CultureScope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }
        public void Dispose()
        {
            CultureInfo.CurrentCulture = old;
            CultureInfo.CurrentUICulture = oldUi;
        }
    }
}
