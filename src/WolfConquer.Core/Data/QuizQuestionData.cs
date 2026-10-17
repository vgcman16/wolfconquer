namespace WolfConquer.Core.Data;

public sealed record QuizQuestionEntry(
    int Position,
    string Question,
    IReadOnlyList<string> Options,
    int CorrectOption,
    string CorrectAnswer,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
