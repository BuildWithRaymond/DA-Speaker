using DASpeaker;
using Xunit;

namespace DASpeaker.Tests;

public sealed class ScriptTests
{
    private static string[] Messages(ScriptPlan p) => p.Steps.Where(s => s.IsMessage).Select(s => s.Text!).ToArray();

    [Fact]
    public void Exact59CharactersRemainOneMessage()
    {
        var text = new string('a', 59);
        var plan = ScriptParser.Parse(text, new());
        Assert.True(plan.IsValid);
        Assert.Equal(new[] { text }, Messages(plan));
        Assert.Single(plan.Steps);
    }

    [Fact]
    public void SixtyCharactersWrapAtSpaceWithoutCuttingWords()
    {
        var plan = ScriptParser.Parse(new string('a', 29) + " " + new string('b', 30), new());
        Assert.True(plan.IsValid);
        Assert.Equal(new[] { new string('a', 29), new string('b', 30) }, Messages(plan));
        Assert.Equal(3500, plan.Steps[1].DelayMs);
    }

    [Fact]
    public void WordOver59IsRejectedWithSourceLocation()
    {
        var plan = ScriptParser.Parse("first\n" + new string('a', 60), new());
        var error = Assert.Single(plan.Errors);
        Assert.Equal(2, error.Line);
        Assert.Equal(6, error.Offset);
        Assert.Equal(60, error.Length);
        Assert.False(plan.IsValid);
    }

    [Fact]
    public void PacksParagraphsPreservingWordsAndPunctuation()
    {
        var plan = ScriptParser.Parse(" Aislings,  gather.\nBe kind!\n\n\nMercy matters.", new());
        Assert.Equal(new[] { "Aislings, gather. Be kind!", "Mercy matters." }, Messages(plan));
        Assert.Equal(5000, plan.Steps[1].DelayMs);
        Assert.Equal("paragraph", plan.Steps[1].Reason);
        Assert.Equal(3, plan.Steps.Count);
    }

    [Theory]
    [InlineData("[wait 5000]")]
    [InlineData("[wait 5s]")]
    public void ExplicitWaitReplacesAutomaticDelaysAndAdjacentBlanks(string directive)
    {
        var plan = ScriptParser.Parse($"One\n\n{directive}\n\nTwo", new());
        Assert.True(plan.IsValid);
        Assert.Equal(new[] { "One", "Two" }, Messages(plan));
        var wait = Assert.Single(plan.Steps, s => !s.IsMessage);
        Assert.Equal(5000, wait.DelayMs);
        Assert.Equal("explicit", wait.Reason);
    }

    [Fact]
    public void ConsecutiveAndLeadingWaitsRemainExplicit()
    {
        var plan = ScriptParser.Parse("\n[wait 100]\n[wait 2s]\nHello\n\n", new());
        Assert.Equal(new[] { 100, 2000 }, plan.Steps.Where(s => !s.IsMessage).Select(s => s.DelayMs));
        Assert.Equal("Hello", plan.Steps.Last().Text);
    }

    [Theory]
    [InlineData("[wait nope]")]
    [InlineData("[wait -1]")]
    [InlineData("[wait 999999999999999999999999]")]
    [InlineData("[wait 2s] text")]
    [InlineData("[wait]")]
    [InlineData("[wait 86400001]")]
    public void MalformedWaitNeverBecomesChat(string directive)
    {
        var plan = ScriptParser.Parse("Hello\n" + directive, new());
        Assert.False(plan.IsValid);
        Assert.Single(plan.Errors);
        Assert.DoesNotContain(Messages(plan), m => m.Contains("[wait"));
    }

    [Fact]
    public void WrapDisabledKeepsSourceLinesAndRejectsLongLines()
    {
        var plan = ScriptParser.Parse("One\nTwo\n" + new string('a', 60), new(false));
        Assert.Equal(new[] { "One", "Two" }, Messages(plan));
        Assert.Single(plan.Errors);
    }

    [Fact]
    public void WrapDisabledPreservesSpacesAndCountsThemAgainstLimit()
    {
        var plan = ScriptParser.Parse("  One  two  \n" + new string('x', 58) + "  ", new(false));
        Assert.Equal(new[] { "  One  two  " }, Messages(plan));
        Assert.Single(plan.Errors);
    }

    [Fact]
    public void CountsSpacesPunctuationAndNormalizesTabs()
    {
        var plan = ScriptParser.Parse("Hi,\tfriend!", new());
        Assert.Equal("Hi, friend!", Assert.Single(Messages(plan)));
        Assert.Equal(11, plan.Steps[0].Text!.Length);
    }

    [Fact]
    public void UnsupportedCharacterHasExactOriginalOffset()
    {
        var plan = ScriptParser.Parse("one\r\ncafé", new());
        var error = Assert.Single(plan.Errors);
        Assert.Equal(8, error.Offset);
        Assert.Equal(2, error.Line);
        Assert.False(plan.IsValid);
    }

    [Fact]
    public void EmptyOrOnlyWaitScriptCannotStart()
    {
        Assert.False(ScriptParser.Parse("\r\n \n", new()).IsValid);
        Assert.False(ScriptParser.Parse("[wait 5s]", new()).IsValid);
    }
}
