using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed partial class RootMagicEntryFixture
    {
        public string EligibilityLog => log.ToString();

        public string DeclareRootPlayerEligibilityInput(ushort id, string denial)
        {
            var owner = Owner;
            var peer = Peer;
            var ownerAttributes = new Dictionary<string, string>(owner.Character.Attributes);
            var peerAttributes = new Dictionary<string, string>(peer.Character.Attributes);
            string reason;
            switch (denial)
            {
                case "hp":
                    peer = peer with { Character = peer.Character with { MinHitPoints = 0 } };
                    reason = "target is dead or ghosted"; break;
                case "dead":
                    peerAttributes["StatusFlags"] = BuildStatusFlags(5);
                    reason = "target is dead or ghosted"; break;
                case "ghost":
                    peerAttributes["StatusFlags"] = BuildStatusFlags(10);
                    reason = "target is dead or ghosted"; break;
                case "invisible":
                    peerAttributes["Invisible"] = bool.TrueString;
                    reason = "target Invisible attribute is set"; break;
                case "curse":
                    peerAttributes["StatusFlags"] = BuildStatusFlags(LegacyNeptuneCurseFlag);
                    reason = "target has NeptuneCurse"; break;
                case "fly":
                    peerAttributes["StatusFlags"] = BuildStatusFlags(0x1B);
                    Assert.IsType<string[]>(NativeRow(id).Source.Fields)[5] = "1";
                    reason = "target is flying and spell AttackInFly is false"; break;
                case "targetWatch":
                    peerAttributes["Watching"] = bool.TrueString;
                    reason = "target watching attribute is set"; break;
                case "casterWatch":
                    ownerAttributes["Watching"] = bool.TrueString;
                    reason = "caster watching attribute is set"; break;
                case "protect":
                    peerAttributes[RuntimeLegacyProtectExpiresAtUtcAttribute] =
                        DateTimeOffset.UtcNow.AddHours(1).ToString("O", CultureInfo.InvariantCulture);
                    reason = "target legacy Protect window is active"; break;
                case "map700":
                    owner = owner with { Character = owner.Character with { MapId = 700 } };
                    peer = peer with { Character = peer.Character with { MapId = 700 } };
                    reason = "target map=700 dynamicId=0"; break;
                case "peace":
                    ownerAttributes["PkMode"] = "1";
                    reason = "caster is in Peace PK mode"; break;
                case "safeZone":
                    owner = owner with { Character = owner.Character with { MapId = 10_137, X = 142 } };
                    peer = peer with { Character = peer.Character with { MapId = 10_137, X = 144 } };
                    reason = "rejected DragonIsland safe zone"; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(denial));
            }
            sessions.Set(connections[PlayerUid], owner with
                { Character = owner.Character with { Attributes = ownerAttributes } });
            sessions.Set(connections[TargetPlayerUid], peer with
                { Character = peer.Character with { Attributes = peerAttributes } });
            return reason;
        }

        public void DeclareRootFlyingTarget()
        {
            var peer = Peer;
            sessions.Set(connections[TargetPlayerUid], peer with
            {
                Character = peer.Character with
                {
                    Attributes = new Dictionary<string, string>(peer.Character.Attributes)
                        { ["StatusFlags"] = BuildStatusFlags(0x1B) },
                },
            });
        }
    }
}
