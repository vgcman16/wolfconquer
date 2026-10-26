namespace WolfConquer.Tests;

internal sealed class OrdinaryAdmissionCommitObserver(TextWriter inner, Action observed) : TextWriter
{
    public override System.Text.Encoding Encoding => inner.Encoding;
    public override void WriteLine(string? value)
    {
        inner.WriteLine(value);
        if (value?.Contains("ordinary admission committed key=", StringComparison.Ordinal) == true) observed();
    }
    public override Task WriteLineAsync(string? value)
    {
        WriteLine(value);
        return Task.CompletedTask;
    }
}
