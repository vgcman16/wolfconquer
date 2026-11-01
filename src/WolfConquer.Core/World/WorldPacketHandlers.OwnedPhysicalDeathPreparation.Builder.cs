using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal enum OwnedPhysicalDeathAudience { Self, SelfAndVisible, Visible, AssignedTeam }

    // Historical calculated output only. This value cannot publish state or
    // send itself; the committed actual Store receipt owns that continuation.
    internal sealed record OwnedPhysicalDeathFrame(OwnedPhysicalDeathAudience Audience,
        ReadOnlyMemory<byte> Bytes, WorldSession Phase, int AssignedHitpoints = 0);

    internal sealed partial class OwnedPhysicalDeathPreparation
    {
        private OwnedPhysicalDeathFrame[] historicalFrames = [];

        private sealed partial class OwnedPhysicalDeathBuilder
        {
            private readonly WorldPacketHandlerContext context;
            private readonly BasicPhysicalReceiveDecision? decision;
            private readonly WorldServerFrameHandler.BasicPhysicalAttempt? attempt;
            private readonly WorldSessionStore.OwnedDragonhowlDeath? dragonhowl;
            private readonly LegacyOrdinarySpellBinding actorBinding, targetBinding;
            private readonly WorldSession actor, target;
            private readonly LegacySpellMutationInputs? sourceInputs;
            private readonly LegacyOwnedPhysicalMetadataWitness? procInputs;
            private readonly LegacyOwnedPhysicalMetadataWitness metadata;
            private readonly List<OwnedPhysicalDeathFrame> frames = [];
            private WorldSession current, querySource;
            private IReadOnlyList<PlayerItemSnapshotItem> equipment = [];
            private bool alternate, admitted;
            private LegacyNativeMagicQueryFields magic, jiangHu;
            private uint prestige;
            private LegacyNativePerfectionSnapshot perfection;
            private uint Uid => CheckedUInt32(targetBinding.Uid, nameof(targetBinding.Uid));
            private WorldDataCatalog? Data => context.WorldData;
            private PlayerItemSnapshot SourceItems => current.PlayerItems with { EquipmentItems = equipment };
            private uint Maximum => GetLegacyEquipmentRuntimeMaxHitpoints(current, Data);

            internal OwnedPhysicalDeathBuilder(WorldPacketHandlerContext context, BasicPhysicalReceiveDecision? decision,
                WorldServerFrameHandler.BasicPhysicalAttempt? attempt, LegacyOrdinarySpellBinding actorBinding,
                LegacyOrdinarySpellBinding targetBinding, WorldSession actor, WorldSession target, WorldSession calculated,
                WorldSessionStore.OwnedDragonhowlDeath? dragonhowl = null)
            {
                this.context = context; this.decision = decision; this.attempt = attempt;
                this.dragonhowl = dragonhowl;
                this.actorBinding = actorBinding; this.targetBinding = targetBinding;
                this.actor = actor; this.target = target;
                nativeStatus = new(targetBinding);
                // This builder privately owns a closed array for each virtual
                // spell view. The original source retains its separate proof;
                // no metadata witness needs to trust a private custom indexer.
                if (dragonhowl is null)
                {
                    sourceInputs = new(calculated, captureItems: true);
                    sourceInputs.ArmPlainProof();
                    current = querySource = sourceInputs.Stable with
                        { PlayerSpells = new PlayerSpellSnapshot(sourceInputs.Spells.Rows.ToArray()) };
                    metadata = LegacyOwnedPhysicalMetadataWitness.Capture(targetBinding,
                        context.WorldData ?? throw InvalidOwnedPhysicalDeath(), current);
                }
                else
                {
                    if (decision is not null || attempt is not null || !dragonhowl.IsCurrentPlain()) throw InvalidOwnedPhysicalDeath();
                    procInputs = LegacyOwnedPhysicalMetadataWitness.CaptureDragonhowl(targetBinding,
                        context.WorldData ?? throw InvalidOwnedPhysicalDeath(), calculated);
                    var copy = CaptureEquipmentQueryInputs(calculated);
                    current = querySource = copy with { PlayerSpells = new PlayerSpellSnapshot(
                        new LegacyPerformerSpellSource(copy.PlayerSpells.Spells).Rows.ToArray()) };
                    metadata = LegacyOwnedPhysicalMetadataWitness.CaptureDragonhowl(targetBinding, context.WorldData, current);
                }
                magic = LegacyNativeMagicQueryClient.Read(targetBinding.Store, targetBinding.Connection,
                    targetBinding.Uid, targetBinding.NativeIdentity);
                jiangHu = LegacyNativeMagicQueryClient.ReadJiangHu(targetBinding.Store, targetBinding.Connection,
                    targetBinding.Uid, targetBinding.NativeIdentity);
                prestige = LegacyNativePerfectionClient.Prestige(targetBinding.Store, targetBinding.Connection,
                    targetBinding.Uid, targetBinding.NativeIdentity);
                perfection = LegacyNativePerfectionClient.Read(targetBinding.Store, targetBinding.Connection,
                    targetBinding.Uid, targetBinding.NativeIdentity);
            }

            internal OwnedPhysicalDeathPreparation Build()
            {
                PrepareDeathPrefix();
                CalculateEquipmentQuery();
                current = current with { Character = nativeStatus.ApplyFinal(current.Character) };
                AssignHitpoints(0);
                current = AddLegacyNormalDeathFlag(current);
                AppendVector();
                RemoveExplicitFlag(LegacyFreezeFlag);
                RemoveExplicitFlag(LegacyTyrantAuraStatusFlag);
                RemoveExplicitFlag(LegacyFendAuraStatusFlag);
                metadata.Extend(current);
                if ((sourceInputs is not null ? !sourceInputs.ConsumeCurrentPlain() : procInputs?.IsCurrentPlain() != true) ||
                    !metadata.IsCurrentPlain()) throw InvalidOwnedPhysicalDeath();
                var values = new OwnedPhysicalDeathQueryValues(admitted, querySource, magic, jiangHu, prestige, perfection);
                var result = new OwnedPhysicalDeathPreparation(context, decision, attempt, actorBinding, targetBinding,
                    actor, target, current, values, nativeStatus, metadata, dragonhowl)
                {
                    historicalFrames = frames.ToArray(),
                    derivedSpells = Array.AsReadOnly(derivedSpells.ToArray()),
                };
                return result;
            }

            private void PrepareDeathPrefix()
            {
                var clock = context.TimeProvider ?? TimeProvider.System;
                var ghostAt = clock.GetUtcNow() + LegacyPlayerDeathState.GhostEffectDelay;
                current = current with { Character = WithCharacterAttribute(current.Character,
                    LegacyPlayerDeathState.GhostStampAttribute, ghostAt.ToString("O")) };
                AppendSelf(LegacyMsgMessagePacket.SystemTalk(LegacyPlayerDeathState.DeadTalkText).ToFrameBytes());
                RemoveExplicitFlag(LegacyBlackNameStatusFlag);
                RemoveExplicitFlag(LegacyFatalStrikeFlag);
                // Deliberately distinct configured clock read, at the source
                // position after DeadTalk and the two explicit removals.
                var deathAt = clock.GetUtcNow();
                var cause = new LegacyNormalPlayerDeathCause(checked((ushort)current.Character.X),
                    checked((ushort)current.Character.Y), actor.Character.Uid, actor.Character.Name);
                current = StageLegacyNormalDeathMetadata(current, cause, actor.Character, deathAt);
                if (HasLegacyStatusFlag(current.Character, LegacyRuntimeUpdateTicker.XpListFlag))
                    AppendSelf(LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(Uid, 0).ToFrameBytes());
                current = ClearLegacyNormalDeathRegistrations(current);
                AppendVector();
            }

            private void RemoveExplicitFlag(int flag)
            {
                if (!HasLegacyStatusFlag(current.Character, flag)) return;
                current = ClearLegacyNormalDeathExplicitFlag(current, flag);
                AppendVector();
            }

            private void AppendSelf(byte[] bytes) =>
                frames.Add(new(OwnedPhysicalDeathAudience.Self, bytes, current));

            private void AppendVector()
            {
                var vector = CaptureCalculatedDeathVector(current);
                frames.Add(new(OwnedPhysicalDeathAudience.SelfAndVisible,
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(Uid, vector).ToFrameBytes(), current));
            }

            private void AssignHitpoints(int value)
            {
                if (value < 0) throw InvalidOwnedPhysicalDeath();
                current = ProjectAssignedLegacyHitpoints(context, current, targetBinding.Uid, value);
                // Actual team membership/identity is captured later by the
                // real receipt. This is a calculated assignment, no fake team.
                frames.Add(new(OwnedPhysicalDeathAudience.AssignedTeam, ReadOnlyMemory<byte>.Empty, current, value));
                AppendHitpointPair();
            }

            private void AppendHitpointPair()
            {
                var maximum = new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, Maximum);
                AppendSelf(new LegacyMsgUpdatePacket(Uid, [maximum]).ToFrameBytes());
                var hitpoints = new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints,
                    unchecked((ulong)current.Character.MinHitPoints));
                AppendSelf(new LegacyMsgUpdatePacket(Uid, [maximum, hitpoints]).ToFrameBytes());
            }

            private void AddHitpoints(uint addition) =>
                AssignHitpoints(RequireHitpoints(checked(current.Character.MinHitPoints + addition)));

            private static int RequireHitpoints(long value) => value is >= 0 and <= int.MaxValue
                ? (int)value : throw InvalidOwnedPhysicalDeath();

            private void SetMaximum(uint value) => current = current with
                { EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(targetBinding.Uid, value) };
            private void AddMaximum(uint value) => SetMaximum(checked(Maximum + value));
            private void AddMana(uint value) => current = current with
            {
                PhysicalStatusOverlay = LegacyPhysicalStatusOverlay.ForOwner(current) with
                    { MaxMana = checked((LegacyPhysicalStatusOverlay.ForOwner(current).MaxMana ?? 0) + value) },
            };
            private void AddMagic(LegacyNativeMagicQueryFields value) => magic = magic with
            {
                MagicAttack = checked(magic.MagicAttack + value.MagicAttack),
                MagicPercent = checked(magic.MagicPercent + value.MagicPercent),
                SkillCStrike = checked(magic.SkillCStrike + value.SkillCStrike),
                MagicDamageIncrease = checked(magic.MagicDamageIncrease + value.MagicDamageIncrease),
            };
            private byte Rune(string name) => Data is { } data &&
                TryGetLegacyEligibleEquippedRuneLevel(current, data, name, out var level) ? level : (byte)0;
        }
    }
}
