using System.Collections.Concurrent;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class OwnedEarlyHawksFixture(OwnedEquipmentBehaviorFixture owner,
        ManualRuntimeTimeProvider clock, bool earnAttackUp)
    {
        internal readonly ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order = new();
        internal Action<uint, byte[]>? Observe;
        internal Dictionary<string, byte[]> BeforeHit = null!;
        internal WorldSession Slot(uint uid) => owner.Current(uid);
        internal int ScheduledTimers => clock.ScheduledTimerCount;

        internal static Task RunAsync(bool earnAttackUp, Func<OwnedEarlyHawksFixture, Task> exercise)
        {
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
            var caster = DirectOverlayItem(111_097, "EarlyShieldCasterHelmet");
            var casterFields = caster.Fields.ToArray(); casterFields[18] = "500"; casterFields[29] = "40400";
            var target = DirectOverlayItem(111_098, "EarlyShieldTargetHelmet");
            var targetFields = target.Fields.ToArray(); targetFields[18] = "500";
            var gearFields = Enumerable.Repeat("0", 22).ToArray();
            gearFields[0] = "701"; gearFields[1] = "701"; gearFields[2] = "1";
            var gear = CreateRootMissingArrowSourceGear() with
            {
                Id = 701, GearTypeId = 701, MagicTypeIds = new int[9],
                SourceFile = "fixture://owned-early-hawks/combat_gear", Fields = gearFields,
                RawLine = string.Join("@@", gearFields) + "@@",
            };
            var catalog = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                new[]
                {
                    CreateMagicRow(1_000, sort: 1, useMana: 0, useStamina: 0, damage: 399,
                        range: 18, coolDown: 0, coldTime: 0),
                    CreateMagicRow(15_750, sort: 6, useMana: 0, useStamina: 0, damage: 100,
                        duration: 30, coolDown: 0, coldTime: 0),
                    CreateMagicRow(DirectEarlyHawksSpell, sort: 6, useMana: 0, useStamina: 0,
                        damage: 50, duration: 60, coolDown: 0, coldTime: 0), DirectEarlyHawksUseRuneRow(),
                }, items: new[]
                {
                    caster with { Fields = casterFields }, target with { Fields = targetFields },
                    DirectEarlyHawksWarmItem(111_099, "EarlyHawksWarmHelmet"),
                    DirectEarlyHawksWarmItem(130_099, "EarlyHawksWarmArmor"),
                }, daoqiDictTypes: new[] { DirectEarlyHawksJade() }, combatGear: new[] { gear });
            var actor = Input(PlayerUid);
            if (earnAttackUp)
                actor = actor with { CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1,
                    Items = [new CombatGearSnapshotItem(701, 1, 0, 1, 0, 0, [[], []], [(uint)DirectEarlyHawksJadeId])],
                } };
            return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(actor, catalog,
                new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async owner =>
                {
                    var fixture = new OwnedEarlyHawksFixture(owner, clock, earnAttackUp);
                    owner.BeforeSend = (uid, bytes) => { fixture.Order.Enqueue((uid, bytes)); fixture.Observe?.Invoke(uid, bytes); };
                    try { await exercise(fixture); }
                    finally { owner.BeforeSend = null; }
                }, Input(TargetPlayerUid), Input(DirectEarlyHawksWarmUid));
        }

        private static OwnedEquipmentBehaviorInput Input(uint uid)
        {
            var session = CreateSession(uid: uid, name: "EarlyH" + uid,
                mapId: 1_000, x: uid == PlayerUid ? 830 : uid == TargetPlayerUid ? 832 : 834, y: 584,
                classCode: uid == PlayerUid ? 7_005 : 2_005, reborn: 2, strength: 0, spirit: 0,
                minHitPoints: uid == PlayerUid ? 500 : uid == TargetPlayerUid ? 300 : 100_000,
                minMana: 1_000, stamina: 100,
                spellIds: uid == PlayerUid ? [1_000, DirectEarlyHawksSpell, DirectEarlyHawksUseRune]
                    : uid == TargetPlayerUid ? [15_750, DirectEarlyHawksSpell] : [],
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["BattlePower"] = "100", ["LegacyLightningShieldRoll"] = "0",
                }, playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = uid == DirectEarlyHawksWarmUid
                        ? [new PlayerItemSnapshotItem { Uid = uid + 100, ItemId = 111_099, Position = 1,
                                Durability = 100, MaximumDurability = 100 },
                            new PlayerItemSnapshotItem { Uid = uid + 101, ItemId = 130_099, Position = 3,
                                Durability = 100, MaximumDurability = 100 }]
                        : [new PlayerItemSnapshotItem { Uid = uid + 100,
                            ItemId = uid == PlayerUid ? 111_097U : 111_098U, Position = 1,
                            Durability = 100, MaximumDurability = 100 }],
                });
            return new(session.Character, session.PlayerItems, session.PlayerSpells, session.PlayerProficiencies);
        }

        private async Task CastAsync(uint actor, uint target, ushort spell)
        {
            clock.Advance(TimeSpan.FromMilliseconds(500));
            await owner.SendAsync(actor, new MsgAttackPacket(new MsgAttackQuery(actor, target,
                checked((ushort)Slot(target).Character.X), checked((ushort)Slot(target).Character.Y),
                MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: spell)).ToFrameBytes());
        }

        internal Task HitAsync() => CastAsync(PlayerUid, TargetPlayerUid, 1_000);

        internal async Task PrimeAsync()
        {
            Assert.False(HasLegacyStatusFlag(Slot(PlayerUid).Character.Attributes, LegacyAttackUpFlag));
            var gear = Slot(PlayerUid).CombatGear ?? CombatGearSnapshot.Empty;
            Assert.Equal(earnAttackUp ? 1 : 0, gear.Count);
            if (earnAttackUp)
            {
                var item = Assert.Single(gear.Items);
                Assert.Equal(701U, item.ItemID); Assert.Equal(1U, item.Equip);
                Assert.Equal(new uint[] { DirectEarlyHawksJadeId, 0, 0, 0, 0, 0 }, item.BlackJades);
            }
            else Assert.Empty(gear.Items);
            if (earnAttackUp)
            {
                // A completed real request, with gear saved before bootstrap/admission.
                // Output observers never reenter the Frame's exclusive operation.
                await CastAsync(PlayerUid, DirectEarlyHawksWarmUid, 1_000);
                Assert.True(HasLegacyStatusFlag(Slot(PlayerUid).Character.Attributes, LegacyAttackUpFlag), owner.Log.ToString());
                Assert.Equal("1", Slot(PlayerUid).Character.Attributes["DirectEarlyHawksAttackUpPower"]);
                Assert.InRange(Slot(DirectEarlyHawksWarmUid).Character.MinHitPoints, 1L, 99_999L);
                Assert.Contains(ParseSpellAnimations(Order.Select(frame => frame.Bytes)),
                    packet => packet.AttackerUid == PlayerUid && packet.SpellId == DirectEarlyHawksUseRune);
            }
            await CastAsync(TargetPlayerUid, TargetPlayerUid, 15_750);
            Assert.True(HasLegacyStatusFlag(Slot(TargetPlayerUid).Character.Attributes, LegacyLightningShieldFlag));
            Assert.False(HasLegacyStatusFlag(Slot(TargetPlayerUid).Character.Attributes, LegacyLightningShieldActivatedFlag));
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                await CastAsync(uid, uid, DirectEarlyHawksSpell);
                var session = Slot(uid);
                Assert.True(HasLegacyStatusFlag(session.Character.Attributes, LegacyHawksAmbitionFlag), owner.Log.ToString());
                Assert.Equal("50", session.Character.Attributes["HawksAmbitionPower"]);
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(session.NativeGameClient!.OrdinarySpellBinding);
                using (binding.EnterMutation())
                lock (binding.Client.SpellAdmissionSyncRoot)
                lock (binding.Player.SpellAdmissionSyncRoot)
                {
                    Assert.True(WorldSessionStore.TryCaptureOwnedHawksStatus(binding, session, out var proof));
                    Assert.NotNull(proof); Assert.True(proof.IsCurrentPlain()); Assert.Equal(50U, proof.Power);
                }
                var start = Order.Count;
                await owner.SendAsync(uid, new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes());
                var bytes = Assert.Single(Order.Skip(start), frame => frame.Recipient == uid &&
                    LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out _)).Bytes;
                Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
                Assert.Equal(500U, status.MaxHP); Assert.Equal(uid == PlayerUid ? 40_401U : 1U, status.MagicAttack);
                Assert.Equal(0U, status.MagicDefence); Assert.Equal(0U, status.ItemBless);
                Assert.Equal(0U, status.Fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
                Assert.Equal(0U, status.Fields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
            }
            BeforeHit = owner.Data.Files();
            AssertCurrentOwners();
        }

        internal void AssertCurrentOwners()
        {
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid, DirectEarlyHawksWarmUid })
            {
                var current = Slot(uid);
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
                Assert.True(owner.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.Equal((long)uid, current.AccountId); Assert.Equal((long)uid, current.Character.Uid);
                Assert.Equal(uid, binding.Player.UID);
                Assert.Same(owner.Connection(uid).NativeGameClient, binding.Client);
                Assert.Equal(checked((int)current.Character.MinHitPoints), binding.Player.HitPoints);
                Assert.Equal(checked((uint)current.Character.MinMana), binding.Player.Mana);
                Assert.Equal(LegacyStaminaState.RequireCurrent(current.Character), binding.Player.Stamina);
                Assert.Equal(LegacyActionState.RequireCurrent(current.Character), (uint)binding.Player.Action);
                var items = PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems);
                Assert.Equal(File.ReadAllBytes(owner.ItemPath(uid)), items);
                Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
                Assert.Equal(File.ReadAllBytes(Path.Combine(owner.Data.Spells, uid + ".bin")),
                    PlayerSpellSnapshotLoader.ToLegacyBytes(current.PlayerSpells));
                Assert.Equal(CombatGearSnapshotLoader.ToLegacyText(new CombatGearSnapshotLoader(owner.Data.CombatGear)
                    .LoadByCharacterUid(uid)), CombatGearSnapshotLoader.ToLegacyText(current.CombatGear ?? CombatGearSnapshot.Empty));
                Assert.Equal(File.ReadAllBytes(Path.Combine(owner.Data.Proficiencies, uid + ".bin")),
                    PlayerProficiencySnapshotLoader.ToLegacyBytes(current.PlayerProficiencies));
                Assert.Equal(current.PlayerSpells.Spells.Count, binding.Client.MySpells!.ClientSpells.Count);
                foreach (var spell in current.PlayerSpells.Spells)
                {
                    var native = binding.Client.MySpells.ClientSpells[spell.Id];
                    Assert.Equal(spell.Level, native.Level); Assert.Equal(spell.Experience, native.Experience);
                    Assert.Equal(spell.PreviousLevel, native.PreviousLevel); Assert.Equal(spell.SoulLevel, native.SoulLevel);
                    Assert.Equal(spell.UseSpellSoul, native.UseSpellSoul);
                }
            }
            Assert.False(LegacyTradeJournal.HasPending(owner.Data.Users));
            Assert.False(owner.Recovery.IsBlocked);
        }

        internal void AssertFiles(bool killed)
        {
            var expected = BeforeHit.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            if (killed)
            {
                var key = Path.GetRelativePath(owner.Data.Root, owner.CharacterPath(TargetPlayerUid));
                var text = Encoding.UTF8.GetString(expected[key]);
                const string from = "MinHitPoints=300\r\n";
                var start = text.IndexOf(from, StringComparison.Ordinal);
                Assert.True(start >= 0); Assert.Equal(-1, text.IndexOf(from, start + from.Length, StringComparison.Ordinal));
                expected[key] = Encoding.UTF8.GetBytes(text[..start] + "MinHitPoints=0\r\n" + text[(start + from.Length)..]);
            }
            owner.Data.AssertFiles(expected);
        }
    }
}
