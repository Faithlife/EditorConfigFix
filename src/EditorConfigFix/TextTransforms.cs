using System.Text;
using EditorConfig.Core;

namespace EditorConfigFix;

internal static class TextTransforms
{
	public static string NormalizeLineEndings(string text, EndOfLine endOfLine)
	{
		var replacement = ToLineEnding(endOfLine);
		var builder = new StringBuilder(text.Length);

		for (var index = 0; index < text.Length; index++)
		{
			var current = text[index];
			if (current == '\r')
			{
				if (index + 1 < text.Length && text[index + 1] == '\n')
					index++;

				builder.Append(replacement);
			}
			else if (current == '\n')
			{
				builder.Append(replacement);
			}
			else
			{
				builder.Append(current);
			}
		}

		return builder.ToString();
	}

	public static string TrimTrailingWhitespace(string text)
	{
		var builder = new StringBuilder(text.Length);
		var pendingWhitespaceStart = -1;
		var pendingWhitespaceLength = 0;

		for (var index = 0; index < text.Length; index++)
		{
			var current = text[index];
			if (current is ' ' or '\t')
			{
				if (pendingWhitespaceStart == -1)
					pendingWhitespaceStart = index;

				pendingWhitespaceLength++;
				continue;
			}

			if (current == '\r')
			{
				pendingWhitespaceStart = -1;
				pendingWhitespaceLength = 0;
				builder.Append('\r');
				if (index + 1 < text.Length && text[index + 1] == '\n')
				{
					builder.Append('\n');
					index++;
				}

				continue;
			}

			if (current == '\n')
			{
				pendingWhitespaceStart = -1;
				pendingWhitespaceLength = 0;
				builder.Append('\n');
				continue;
			}

			AppendPendingWhitespace(builder, text, pendingWhitespaceStart, pendingWhitespaceLength);
			pendingWhitespaceStart = -1;
			pendingWhitespaceLength = 0;
			builder.Append(current);
		}

		return builder.ToString();
	}

	public static string ApplyFinalNewline(string text, EndOfLine? configuredEndOfLine)
	{
		if (text.Length == 0)
			return text;

		var lineEnding = configuredEndOfLine is { } endOfLine ? ToLineEnding(endOfLine) : FindFirstLineEnding(text) ?? Environment.NewLine;
		return RemoveTerminalLineBreaks(text) + lineEnding;
	}

	public static string ToLineEnding(EndOfLine endOfLine) => endOfLine switch
	{
		EndOfLine.CR => "\r",
		EndOfLine.CRLF => "\r\n",
		EndOfLine.LF => "\n",
		_ => throw new InvalidOperationException($"Unsupported end_of_line value: {endOfLine}"),
	};

	private static void AppendPendingWhitespace(StringBuilder builder, string text, int startIndex, int length)
	{
		if (length != 0)
			builder.Append(text, startIndex, length);
	}

	private static string? FindFirstLineEnding(string text)
	{
		for (var index = 0; index < text.Length; index++)
		{
			if (text[index] == '\r')
				return index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";

			if (text[index] == '\n')
				return "\n";
		}

		return null;
	}

	private static string RemoveTerminalLineBreaks(string text)
	{
		var end = text.Length;
		while (end > 0)
		{
			if (end >= 2 && text[end - 2] == '\r' && text[end - 1] == '\n')
			{
				end -= 2;
			}
			else if (text[end - 1] is '\r' or '\n')
			{
				end--;
			}
			else
			{
				break;
			}
		}

		return text[..end];
	}
}