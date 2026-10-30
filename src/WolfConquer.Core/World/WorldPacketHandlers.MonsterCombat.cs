using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsLegacyExtraBoss(LegacyMonsterInstance monster)
    {
        return monster.Boss &&
            monster.MaxLife > 300_000 &&
            monster.MaxLife < 7_000_000;
    }

    private static bool HasLegacySourceSpecialDropBranch(LegacyMonsterInstance monster, Monster monsterFamily)
    {
        if (monsterFamily.Id < 0 || monster.MapId < 0)
        {
            return false;
        }

        return LegacyBossSpecialDeathDropPolicy.IsSourceSpecialDropBranch(
            checked((uint)monsterFamily.Id),
            checked((uint)monster.MapId),
            monster.Boss,
            monsterFamily.Name);
    }

    private static async ValueTask<WorldSession> TryApplyLegacyMonsterExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        uint requestedDamage,
        CancellationToken cancellationToken)
    {
        if (requestedDamage == 0 ||
            LegacyMonsterDamageRules.IsLegacyGuardFamily(monsterFamily))
        {
            return session;
        }

        var adjustedExperience = AdjustLegacyMonsterExperience(
            checked((int)Math.Min(requestedDamage, monster.MaxLife)),
            checked((int)session.Character.Level),
            monster.Level);
        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, adjustedExperience));
        var updatedSession = ApplyLegacyExperienceSideEffects(context, context.Connection, session, experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            if (experienceResult.OblivionExperienceGain != 0 ||
                experienceResult.HuntingBlessingGain != 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy monster experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                        $"baseGain={adjustedExperience} applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} suppression={experienceResult.SuppressionReason}; " +
                        "source=AttackHandler.Updates.IncreaseExperience.Up.")
                    .ConfigureAwait(false);
            }

            return updatedSession;
        }

        updatedSession = await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer sent legacy monster experience " +
                $"{FormatSession(context with { Session = updatedSession })} monsterUid={monster.Uid} " +
                $"baseGain={adjustedExperience} gain={experienceResult.AppliedExperienceGain} " +
                $"oblivion={experienceResult.OblivionExperienceGain} huntingBlessing={experienceResult.HuntingBlessingGain} " +
                $"experience={updatedSession.Character.Experience} level={updatedSession.Character.Level}; " +
                "source=AttackHandler.Updates.IncreaseExperience.Up -> GameClient.IncreaseExperience.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask TryApplyLegacyMonsterTeamExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession killerSession,
        LegacyMonsterInstance monster,
        CancellationToken cancellationToken)
    {
        if (context.TeamStates is not { } teamStates ||
            context.Sessions is not { } sessions)
        {
            return;
        }

        var teamConnections = teamStates.GetConnections(context.Connection);
        if (teamConnections.Count <= 1)
        {
            return;
        }

        var activeMembers = new List<(ConquerTcpConnection Connection, WorldSession Session, LegacyMonsterTeamExperienceShareMember Member)>(
            teamConnections.Count);
        for (var index = 0; index < teamConnections.Count; index++)
        {
            var connection = teamConnections[index];
            WorldSession? memberSession;
            if (ReferenceEquals(connection, context.Connection))
            {
                memberSession = killerSession;
            }
            else if (!sessions.TryGet(connection, out memberSession) || memberSession is null)
            {
                continue;
            }

            activeMembers.Add((
                connection,
                memberSession,
                CreateLegacyMonsterTeamExperienceShareMember(memberSession.Character)));
        }

        if (activeMembers.Count <= 1)
        {
            return;
        }

        var target = new LegacyMonsterTeamExperienceShareTarget(
            checked((int)monster.MaxLife),
            monster.Level,
            monster.MapId,
            monster.X,
            monster.Y);
        var members = activeMembers.Select(member => member.Member).ToArray();
        var computedShare = LegacyMonsterTeamExperienceShare.CalculateComputedShare(target, members);
        if (computedShare <= 0)
        {
            return;
        }

        var killer = CreateLegacyMonsterTeamExperienceShareMember(killerSession.Character);
        var killerSpouseUid = ReadAttributeUInt32(
            killerSession.Character.Attributes,
            "SpouseUID",
            "SpouseUid",
            "SpouseID",
            "SpouseId");
        var appliedCount = 0;
        foreach (var (connection, memberSession, member) in activeMembers)
        {
            var amount = LegacyMonsterTeamExperienceShare.CalculateRecipientAmount(
                computedShare,
                member,
                killer,
                killerSpouseUid);
            if (amount <= 0)
            {
                continue;
            }

            var updatedSession = await ApplyLegacyExperienceGainAsync(
                    context,
                    connection,
                    memberSession,
                    checked((ulong)amount),
                    cancellationToken)
                .ConfigureAwait(false);
            if (updatedSession.Character.Experience != memberSession.Character.Experience ||
                updatedSession.Character.Level != memberSession.Character.Level)
            {
                appliedCount++;
            }
        }

        if (appliedCount > 0)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer sent legacy monster team experience " +
                    $"{FormatSession(context)} monsterUid={monster.Uid} share={computedShare} recipients={appliedCount}.")
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask<WorldSession> ApplyLegacyExperienceGainAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        ulong experienceGain,
        CancellationToken cancellationToken)
    {
        if (experienceGain == 0)
        {
            return session;
        }

        var experienceResult = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            CreateLegacyExperienceGainInputs(context, session, experienceGain));
        if (experienceResult.AppliedExperienceGain == 0)
        {
            return session;
        }

        return await ApplyLegacyFinalExperienceGainAsync(
                context,
                connection,
                session,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession> ApplyLegacyFinalExperienceGainAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        ulong experienceGain,
        CancellationToken cancellationToken)
    {
        if (experienceGain == 0)
        {
            return session;
        }

        if (experienceGain > long.MaxValue)
        {
            throw new OverflowException("Legacy experience gain cannot fit in the local character snapshot.");
        }

        var updatedExperience = checked(session.Character.Experience + (long)experienceGain);
        var updatedLevel = session.Character.Level;
        var levelChanged = false;
        while (updatedLevel < 140 &&
            context.WorldData?.LevelExperienceByTypeAndLevel.TryGetValue((0, checked((int)updatedLevel)), out var levelExperience) == true &&
            levelExperience.Experience > 0 &&
            updatedExperience >= levelExperience.Experience)
        {
            updatedExperience -= levelExperience.Experience;
            updatedLevel++;
            levelChanged = true;
        }

        var leveledCharacter = session.Character with
        {
            Experience = updatedLevel >= 140 ? 0 : updatedExperience,
            Level = updatedLevel,
        };
        var updatedCharacter = LegacyClassLevelStatProgression.Apply(
            leveledCharacter,
            context.WorldData,
            session.Character.Level,
            out var statProgression);
        if (levelChanged)
        {
            updatedCharacter = LegacyClassLevelStatProgression.RefreshVitals(
                updatedCharacter,
                session.PlayerItems,
                session.CombatGear ?? CombatGearSnapshot.Empty,
                context.WorldData);
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(connection, updatedSession);

        var uid = CheckedUInt32(updatedCharacter.Uid, nameof(updatedCharacter.Uid));
        if (levelChanged)
        {
            await connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeLevelUpdate(uid, checked((ulong)updatedLevel)).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (statProgression.AppliedSourceStats)
            {
                foreach (var update in LegacyMsgUpdatePacket.CreateLegacyRuntimeSourceStatUpdates(uid, updatedCharacter))
                {
                    await connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (statProgression.AddedAttributePoints)
            {
                await connection.SendAsync(
                        LegacyMsgUpdatePacket.CreateLegacyRuntimeAttributePointsUpdate(
                                uid,
                                CheckedUInt64(updatedCharacter.AttributePoints, nameof(updatedCharacter.AttributePoints)))
                            .ToFrameBytes(),
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerHitpointUpdatePair(
                         uid,
                         CheckedUInt32(updatedCharacter.MinHitPoints, nameof(updatedCharacter.MinHitPoints)),
                         CheckedUInt32(updatedCharacter.MinHitPoints, nameof(updatedCharacter.MinHitPoints))))
            {
                await connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var update in LegacyMsgUpdatePacket.CreateLegacyPlayerManaUpdatePair(
                         uid,
                         CheckedUInt64(updatedCharacter.MaxMana, nameof(updatedCharacter.MaxMana)),
                         CheckedUInt64(updatedCharacter.MinMana, nameof(updatedCharacter.MinMana))))
            {
                await connection.SendAsync(update.ToFrameBytes(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        await connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeExperienceUpdate(
                        uid,
                        checked((ulong)updatedCharacter.Experience))
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        return updatedSession;
    }

    private static LegacyExperienceGainInputs CreateLegacyExperienceGainInputs(
        WorldPacketHandlerContext context,
        WorldSession session,
        double baseAmount)
    {
        var hasSoulChant = TryGetLegacySoulChantRuneLevel(session, context.WorldData, out var soulChantLevel);
        return new LegacyExperienceGainInputs(
            baseAmount,
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            CheckedInt32(ReadAttributeUInt32(session.Character.Attributes, "CursedTimer"), "CursedTimer"),
            hasSoulChant,
            soulChantLevel,
            ReadLegacyServerExperienceRate(session.Character),
            CalculateLegacyRainbowGemPercent(session.PlayerItems, session.AlternateEquipmentActive),
            CheckedInt32(
                ReadAttributeUInt32(
                    session.Character.Attributes,
                    "DExpTime",
                    "DoubleExperienceTime",
                    "DoubleExpTime"),
                "DExpTime"),
            ReadLegacyDoubleExperienceRate(session.Character),
            CheckedInt32(session.Character.MapId, nameof(session.Character.MapId)),
            HasLegacyStatusFlag(session.Character, LegacyOblivionFlag),
            HasLegacyStatusFlag(session.Character, LegacyHeavenBlessingFlag) ||
                ReadAttributeUInt64(session.Character.Attributes, "HeavenBlessing", "HeavensBlessing") > 0);
    }

    private static WorldSession ApplyLegacyExperienceSideEffects(
        WorldPacketHandlerContext context,
        ConquerTcpConnection connection,
        WorldSession session,
        LegacyExperienceGainResult result)
    {
        var updatedCharacter = session.Character;
        var changed = false;
        if (result.OblivionExperienceGain != 0)
        {
            var current = ReadAttributeUInt64(
                updatedCharacter.Attributes,
                "ExpOblivion",
                "OblivionExperience");
            var updated = unchecked(current + result.OblivionExperienceGain);
            updatedCharacter = WithCharacterAttribute(
                WithCharacterAttribute(updatedCharacter, "ExpOblivion", updated),
                "OblivionExperience",
                updated);
            changed = true;
        }

        if (result.HuntingBlessingGain != 0)
        {
            var current = ReadAttributeUInt32(
                updatedCharacter.Attributes,
                "HuntingBlessing",
                "HuntBlessing");
            updatedCharacter = WithLegacyHuntBlessing(
                updatedCharacter,
                unchecked(current + result.HuntingBlessingGain));
            changed = true;
        }

        if (!changed)
        {
            return session;
        }

        var updatedSession = session with { Character = updatedCharacter };
        context.Sessions?.Set(connection, updatedSession);
        return updatedSession;
    }

    private static double ReadLegacyServerExperienceRate(CharacterSnapshot character)
    {
        return LegacyExperienceGainPolicy.ReadServerExperienceRate(character.Attributes);
    }

    private static double ReadLegacyDoubleExperienceRate(CharacterSnapshot character)
    {
        return TryReadDoubleAttribute(
            character.Attributes,
            out var rate,
            "RateExp",
            "DoubleExperienceRate",
            "DExpRate")
            ? rate
            : 1.0;
    }

    private static int CalculateLegacyRainbowGemPercent(
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive)
    {
        var percent = 0;
        foreach (var item in LegacyEquipmentSelector.SelectCurrentEquipment(playerItems, alternateEquipmentActive))
        {
            if (item.Durability == 0)
            {
                continue;
            }

            percent += GetLegacyRainbowGemPercent(item.SocketOne);
            percent += GetLegacyRainbowGemPercent(item.SocketTwo);
        }

        return percent;
    }

    private static int GetLegacyRainbowGemPercent(byte socket)
    {
        return socket switch
        {
            LegacyNormalRainbowGem => 10,
            LegacyRefinedRainbowGem => 15,
            LegacySuperRainbowGem => 25,
            _ => 0,
        };
    }

    private static bool TryGetLegacySoulChantRuneLevel(
        WorldSession session,
        WorldDataCatalog? worldData,
        out byte level)
    {
        if (TryReadUInt32Attribute(
            session.Character.Attributes,
            out var attributeLevel,
            "SoulChantLevel",
            "SoulChantRuneLevel",
            "RuneSoulChantLevel"))
        {
            level = checked((byte)Math.Min(attributeLevel, byte.MaxValue));
            return true;
        }

        level = 0;
        foreach (var rune in session.PlayerItems.RuneItems)
        {
            if (!IsLegacyWornRunePosition(rune.Position) ||
                !TryGetLegacyItemType(worldData?.ItemsById, rune.ItemId, out var itemType) ||
                !ContainsLegacyItemText(itemType, "SoulChant"))
            {
                continue;
            }

            level = checked((byte)(rune.ItemId % 100));
            return true;
        }

        return false;
    }

    private static bool TryGetLegacyItemType(
        IReadOnlyDictionary<int, IReadOnlyList<ItemType>>? itemsById,
        uint itemId,
        out ItemType itemType)
    {
        itemType = default!;
        if (itemId > int.MaxValue ||
            itemsById is null ||
            !itemsById.TryGetValue((int)itemId, out var rows) ||
            rows.Count == 0)
        {
            return false;
        }

        itemType = rows[0];
        return true;
    }

    private static bool ContainsLegacyItemText(ItemType itemType, string text)
    {
        if (itemType.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (var index = 0; index < itemType.Fields.Count; index++)
        {
            if (itemType.Fields[index].Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLegacyWornRunePosition(ushort position)
    {
        return position is >= 101 and <= 131;
    }

    private static LegacyMonsterTeamExperienceShareMember CreateLegacyMonsterTeamExperienceShareMember(
        CharacterSnapshot character)
    {
        return new LegacyMonsterTeamExperienceShareMember(
            CheckedUInt32(character.Uid, nameof(character.Uid)),
            CheckedInt32(character.Level, nameof(character.Level)),
            CheckedInt32(character.MapId, nameof(character.MapId)),
            CheckedUInt16(character.X, nameof(character.X)),
            CheckedUInt16(character.Y, nameof(character.Y)),
            IsLegacyAlive(character));
    }

    private static bool IsLegacyAlive(CharacterSnapshot character)
    {
        return character.MinHitPoints > 0 &&
            !HasLegacyStatusFlag(character, LegacyDeadFlag) &&
            !HasLegacyStatusFlag(character, LegacyGhostFlag);
    }

    private static int AdjustLegacyMonsterExperience(int damage, int attackerLevel, int monsterLevel)
    {
        var adjusted = damage;
        var nameType = GetLegacyMonsterNameType(attackerLevel, monsterLevel);
        var levelDelta = attackerLevel - monsterLevel;
        switch (nameType)
        {
            case 1:
                if (levelDelta >= 3 && levelDelta <= 5)
                {
                    adjusted = adjusted * 70 / 100;
                }
                else if (levelDelta > 5 && levelDelta <= 10)
                {
                    adjusted = adjusted * 20 / 100;
                }
                else if (levelDelta > 10 && levelDelta <= 20)
                {
                    adjusted = adjusted * 10 / 100;
                }
                else if (levelDelta > 20)
                {
                    adjusted = adjusted * 5 / 100;
                }

                break;
            case 3:
                adjusted = (int)(adjusted * 1.3);
                break;
            case 4:
                if (levelDelta >= -10 && levelDelta < -5)
                {
                    adjusted = (int)(adjusted * 1.5);
                }
                else if (levelDelta >= -20 && levelDelta < -10)
                {
                    adjusted = (int)(adjusted * 1.8);
                }
                else if (levelDelta < -20)
                {
                    adjusted = (int)(adjusted * 2.3);
                }

                break;
        }

        return Math.Max(10, adjusted);
    }

    private readonly record struct LegacyLightningShieldMonsterHitResult(
        WorldSession Session,
        uint Damage);

    private static ValueTask<WorldSession> TryConsumeLegacyHawksAmbitionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool shouldConsume,
        CancellationToken cancellationToken)
    {
        return TryConsumeLegacyHawksAmbitionAsync(
            context,
            context.Connection,
            session,
            shouldConsume,
            cancellationToken);
    }

    private static async ValueTask<WorldSession> TryConsumeLegacyHawksAmbitionAsync(
        WorldPacketHandlerContext context,
        ConquerTcpConnection subjectConnection,
        WorldSession session,
        bool shouldConsume,
        CancellationToken cancellationToken)
    {
        if (!shouldConsume || !HasLegacyStatusFlag(session.Character, LegacyHawksAmbitionFlag))
        {
            return session;
        }

        var updatedSession = session with
        {
            Character = WithoutLegacyStatusFlag(session.Character, LegacyHawksAmbitionFlag),
        };
        context.Sessions?.Set(subjectConnection, updatedSession);
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                subjectConnection,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer consumed legacy HawksAmbition monster-hit reduction " +
                $"{FormatSession(context with { Session = updatedSession })} statusFlag={LegacyHawksAmbitionFlag}; " +
                "source=MsgMonster.ActionHandler PhysicalAttack/MagicAttack RemoveFlag(HawksAmbition).")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint GetLegacyFlashShieldBonus(CharacterSnapshot character)
    {
        var itemLevel = ReadAttributeUInt32(character.Attributes, "FlashShieldLevel", "LegacyFlashShieldLevel");
        return itemLevel switch
        {
            0 => 0,
            1 => 5_000,
            2 => 6_000,
            3 => 7_000,
            >= 4 and <= 9 => 5_500 + (itemLevel * 500),
            >= 10 and <= 26 => 6_000 + (itemLevel * 500),
            >= 27 => 20_000,
        };
    }

    private static string FormatLegacyMonsterPhysicalDamageInputs(
        LegacyMonsterInstance monster,
        Monster monsterFamily,
        LegacyMonsterPhysicalDamageResult result)
    {
        return $"familyId={monsterFamily.Id} monsterName={monster.Name} " +
            $"minAttack={result.MonsterMinAttack} maxAttack={result.MonsterMaxAttack} " +
            $"selectedAttack={result.SelectedAttack} physicalDefense={result.PlayerPhysicalDefense} " +
            $"adjustedPhysicalDefense={result.PlayerAdjustedPhysicalDefense} " +
            $"itemBless={result.PlayerItemBless} tortoiseDamage={result.PlayerTortoiseDamagePercent} " +
            "source=MsgMonster.ActionHandler.PhysicalAttack.";
    }

    private static async ValueTask<WorldSession> TryConsumeLegacySlayerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        bool shouldConsume,
        CancellationToken cancellationToken)
    {
        if (!shouldConsume || !HasLegacyStatusFlag(session.Character, LegacySlayerFlag))
        {
            return session;
        }

        var updatedSession = session with
        {
            Character = WithoutLegacyStatusFlag(session.Character, LegacySlayerFlag),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                context.Connection,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer consumed legacy Slayer monster-hit damage flag " +
                $"{FormatSession(context with { Session = updatedSession })} statusFlag={LegacySlayerFlag}; " +
                "source=Magic.OnMonster/Physical.OnPlayer/OnNpcs RemoveFlag(Slayer).")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static bool TryReadLegacyMsgNinjaEquippedItemLevel(
        string raw,
        int itemType,
        bool alternateEquipmentActive,
        out uint level)
    {
        level = 0;
        var fields = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length <= LegacyMsgNinjaHeaderFieldCount)
        {
            return false;
        }

        for (var index = LegacyMsgNinjaHeaderFieldCount;
             index + 2 < fields.Length;
             index += LegacyMsgNinjaItemRecordFieldCount)
        {
            if (!uint.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentType) ||
                currentType != itemType ||
                !uint.TryParse(fields[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentLevel) ||
                currentLevel == 0 ||
                !uint.TryParse(fields[index + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var position) ||
                !IsLegacyNinjaGouyuEquippedPosition(position, alternateEquipmentActive))
            {
                continue;
            }

            level = currentLevel;
            return true;
        }

        return false;
    }

    private static bool IsLegacyNinjaGouyuEquippedPosition(uint position, bool alternateEquipmentActive)
    {
        return alternateEquipmentActive
            ? position is >= 1 and <= 16
            : position is >= 1 and <= 8;
    }





    private static uint[] CalculateLegacyRuntimeStatusFields(
        WorldSession session,
        WorldDataCatalog? worldData)
    {
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        var maxHitpoints = GetLegacyEquipmentRuntimeMaxHitpoints(session, worldData);
        var refineLevel = LegacyUserTotalRefineLevPacket.FromEquipmentPerfection(uid, session.PlayerItems);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            maxHitpoints,
            refineLevel.Level,
            combatGear,
            alternateEquipmentActive: session.AlternateEquipmentActive,
            playerSpells: session.PlayerSpells,
            protocolProfile: GetLegacyRuntimeItemProfile(session, worldData));
        ApplyLegacyRuntimeStatusFieldOverrides(session.Character, fields);
        var overlay = LegacyPhysicalStatusOverlay.ForOwner(session);
        if (overlay.Breakthrough is { } breakthrough)
        {
            fields[LegacyPlayerStatusPacket.BreakthroughIndex] = breakthrough;
        }

        if (overlay.Counteraction is { } counteraction)
        {
            fields[LegacyPlayerStatusPacket.CounteractionIndex] = counteraction;
        }
        if (overlay.MaxMana is { } maxMana)
        {
            fields[LegacyPlayerStatusPacket.MaxManaIndex] = maxMana;
        }

        return fields;
    }

    private static ConquerProtocolProfile? GetLegacyRuntimeItemProfile(WorldSession session, WorldDataCatalog? worldData)
    {
        // Existing projections without this exact passive retain their prior
        // behavior; a stale owner cannot silently downgrade an applicable buff.
        if (!session.PlayerItems.RuneItems.Any(item => item.ItemId is >= 4_020_201 and <= 4_020_227)) return null;
        if (session.NativeGameClient?.OrdinarySpellBinding is not { } binding) return null;
        if (!ReferenceEquals(binding.ItemCatalog, worldData) || !binding.Store.IsBoundSpellOwnerCurrent(binding))
            throw new InvalidOperationException("Runtime status requires the current adopted item owner.");
        return binding.ItemProfile;
    }










}
