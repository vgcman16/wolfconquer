using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Existing native-game login only. Local-file compatibility plus conditional Beast/Hairface birth,
    // Hairface saved-state and native770..788 fields. Not SQL or LoadCharacter789+ state.
    private bool TryApplyLegacyExistingCharacterLocalPrefix(
        ConquerTcpConnection connection,
        CharacterSnapshot snapshot,
        out CharacterSnapshot updatedSnapshot,
        out string reason)
    {
        updatedSnapshot = snapshot;
        reason = string.Empty;
        if (connection.NativeGameClient is not { } nativeClient)
        {
            return true;
        }

        nativeClient.Player.UID = checked((uint)snapshot.Uid);
        var path = Path.Combine(
            _snapshotLoader!.UsersRoot,
            snapshot.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(
                path,
                expectedCharacterUid: snapshot.Uid,
                values: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Online"] = "1" },
                detailedIoFailures: false,
                out reason))
        {
            return false;
        }

        // Native bool.Parse/defaultfalse semantics for ordinary parsed attribute values.
        nativeClient.Player.RelicSpirit =
            snapshot.Attributes.TryGetValue("RelicSpirit", out var rawRelicSpirit) &&
            bool.TryParse(rawRelicSpirit, out var relicSpirit) && relicSpirit;
        unchecked
        {
            nativeClient.Player.Online++;
        }

        if (nativeClient.Beasts is null)
        {
            nativeClient.Beasts = new LegacyNativeBeastsRuntime(nativeClient);
        }

        if (nativeClient.HairfaceStorage is null)
        {
            nativeClient.HairfaceStorage = new LegacyNativeHairfaceStorageRuntime(nativeClient);
        }

        // ServerDatabase.LoadCharacter769, after both conditional births. Use the
        // physical native INI reader's default255 buffer, not snapshot attribute text.
        var nativeIni = new LegacyNativeItemCounterShellIni(path, overridePath: true);
        nativeClient.HairfaceStorage.Load(nativeIni.ReadString("Character", "HairfaceStorage", "0/"));
        nativeClient.Beasts.Activated = nativeIni.ReadBool("Character", "TailedBeastActivated", false);
        nativeClient.Player.SpawinEffect = nativeIni.ReadBool("Character", "SpawinEffect", false);
        nativeClient.Player.SpawinEffectSkills = nativeIni.ReadBool("Character", "SpawinEffectSkills", false);
        nativeClient.Beasts.TotalPoints = nativeIni.ReadUInt32("Character", "TailedBeastTotalPoints");
        nativeClient.Beasts.flag = nativeIni.ReadUInt32("Character", "TailedBeastFlag");
        nativeClient.Player.DeityLandLuckyPoints = nativeIni.ReadUInt32("Character", "DeityLandLuckyPoints");
        nativeClient.Player.HeroPoints = nativeIni.ReadUInt32("Character", "HeroPoints");
        nativeClient.Player.ClassExperience = nativeIni.ReadUInt32("Character", "ClassExperience");
        nativeClient.Player.SavePromote = nativeIni.ReadUInt32("Character", "SavePromote");
        nativeClient.Player.SkinUnlock = nativeIni.ReadByte("Character", "SkinUnlock", 0);
        nativeClient.Player.DoomosaurSkin = nativeIni.ReadByte("Character", "DoomosaurSkin", 0);
        nativeClient.Player.PrayingElfSkin = nativeIni.ReadByte("Character", "PrayingElfSkin", 0);
        nativeClient.Player.PolarDragonSkin = nativeIni.ReadByte("Character", "PolarDragonSkin", 0);
        nativeClient.Player.ChargeChi = nativeIni.ReadByte("Character", "ChargeChi", 0);
        nativeClient.Player.ChargeGuildSkill = nativeIni.ReadByte("Character", "ChargeGuildSkill", 0);
        nativeClient.Player.Body = nativeIni.ReadUInt16("Character", "Body", 1002);
        nativeClient.Player.Face = nativeIni.ReadUInt16("Character", "Face", 0);
        nativeClient.Player.Name = nativeIni.ReadString("Character", "Name", "None");
        nativeClient.Player.Spouse = nativeIni.ReadString("Character", "Spouse", "None");

        // The persisted marker is 1, not the distinct physical counter increment.
        updatedSnapshot = snapshot with
        {
            Online = 1,
            Attributes = new Dictionary<string, string>(snapshot.Attributes, StringComparer.OrdinalIgnoreCase)
            {
                ["Online"] = "1",
            },
        };
        return true;
    }
}
