namespace WolfConquer.Core.Data;

public sealed record CharacterSnapshot(
    long Uid,
    string Name,
    long Body,
    long Face,
    long Hair,
    long Class,
    long FirstClass,
    long SecondClass,
    long Reborn,
    long Level,
    long Experience,
    long MapId,
    long X,
    long Y,
    long PreviousMapId,
    long PreviousMapX,
    long PreviousMapY,
    long Strength,
    long Agility,
    long Vitality,
    long Spirit,
    long AttributePoints,
    long MinHitPoints,
    long MinMana,
    long Money,
    long ConquerPoints,
    long BoundConquerPoints,
    long BankConquerPoints,
    long PkPoints,
    long Online,
    string SourceFile,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<string, string> PaidNobility)
{
    public long ExpBallUsed { get; init; }
    public long MaxMana { get; init; } = MinMana;
    public PlayerQuestSnapshot PlayerQuests { get; init; } = PlayerQuestSnapshot.Empty;
}
