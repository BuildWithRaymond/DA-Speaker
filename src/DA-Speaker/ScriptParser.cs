using System.Globalization;
using System.Text.RegularExpressions;

namespace DASpeaker;

internal sealed record ScriptOptions(bool AutoWrap = true, int LineDelayMs = 3500, int ParagraphDelayMs = 5000);
internal sealed record ScriptError(int Line, int Offset, int Length, string Message);
internal sealed record QueueStep(string? Text, int DelayMs, int SourceLine, string Reason)
{
    public bool IsMessage => Text is not null;
}
internal sealed record ScriptPlan(IReadOnlyList<QueueStep> Steps, IReadOnlyList<ScriptError> Errors)
{
    public int MessageCount => Steps.Count(s => s.IsMessage);
    public bool IsValid => Errors.Count == 0 && MessageCount > 0;
}

internal static class ScriptParser
{
    public static ScriptPlan Parse(string script, ScriptOptions options)
    {
        if (options.LineDelayMs is < 0 or > 86400000 || options.ParagraphDelayMs is < 0 or > 86400000)
            throw new ArgumentOutOfRangeException(nameof(options));
        var raw = new List<QueueStep>();
        var errors = new List<ScriptError>();
        var words = new List<(string Text, int Line, int Offset)>();
        var lineNumber = 0;
        foreach (Match line in Regex.Matches(script, @"[^\r\n]*(?:\r\n|\r|\n|$)"))
        {
            if (line.Length == 0) break;
            lineNumber++;
            var content = line.Value.TrimEnd('\r', '\n');
            var trimmed = content.Trim(' ', '\t');
            if (trimmed.Length == 0)
            {
                FlushWords();
                raw.Add(new(null, 0, lineNumber, "paragraph"));
                continue;
            }
            if (trimmed.StartsWith('['))
            {
                FlushWords();
                var command = Regex.Match(trimmed, @"^\[wait[ \t]+([0-9]+)(s)?\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!command.Success || !long.TryParse(command.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) ||
                    amount > 86400000L / (command.Groups[2].Success ? 1000 : 1))
                    errors.Add(new(lineNumber, line.Index, content.Length, "Invalid directive. Use [wait 5000] or [wait 5s], up to 24 hours."));
                else raw.Add(new(null, checked((int)(amount * (command.Groups[2].Success ? 1000 : 1))), lineNumber, "explicit"));
                continue;
            }
            var invalid = false;
            for (var i = 0; i < content.Length; i++)
                if ((content[i] is < ' ' or > '~') && !(options.AutoWrap && content[i] == '\t'))
                {
                    errors.Add(new(lineNumber, line.Index + i, 1, "Use printable ASCII text; replace this character."));
                    invalid = true;
                }
            if (invalid) continue;
            var lineWords = Regex.Matches(content, @"[^ \t]+").Cast<Match>().ToArray();
            if (!options.AutoWrap)
            {
                if (content.Length > 59) errors.Add(new(lineNumber, line.Index, content.Length, "Line exceeds 59 characters. Enable wrapping or shorten it."));
                else raw.Add(new(content, 0, lineNumber, "message"));
                continue;
            }
            foreach (var word in lineWords)
            {
                if (word.Length > 59) errors.Add(new(lineNumber, line.Index + word.Index, word.Length, "Word exceeds 59 characters. Shorten it; words are never split."));
                else words.Add((word.Value, lineNumber, line.Index + word.Index));
            }
        }
        FlushWords();
        var steps = new List<QueueStep>();
        for (var i = 0; i < raw.Count; i++)
        {
            var current = raw[i];
            if (!current.IsMessage)
            {
                if (current.Reason == "explicit") steps.Add(current);
                continue;
            }
            steps.Add(current);
            var next = i + 1;
            var paragraph = false;
            var explicitWait = false;
            while (next < raw.Count && !raw[next].IsMessage)
            {
                paragraph |= raw[next].Reason == "paragraph";
                explicitWait |= raw[next].Reason == "explicit";
                next++;
            }
            if (next < raw.Count && !explicitWait)
                steps.Add(new(null, paragraph ? options.ParagraphDelayMs : options.LineDelayMs, current.SourceLine, paragraph ? "paragraph" : "line"));
        }
        return new(steps.AsReadOnly(), errors.AsReadOnly());

        void FlushWords()
        {
            var message = "";
            var source = 0;
            foreach (var word in words)
            {
                if (message.Length > 0 && message.Length + 1 + word.Text.Length > 59)
                {
                    raw.Add(new(message, 0, source, "message"));
                    message = "";
                }
                if (message.Length == 0) { source = word.Line; message = word.Text; }
                else message += " " + word.Text;
            }
            if (message.Length > 0) raw.Add(new(message, 0, source, "message"));
            words.Clear();
        }
    }
}
