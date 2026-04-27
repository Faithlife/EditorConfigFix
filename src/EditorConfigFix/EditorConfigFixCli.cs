using System.CommandLine;

namespace EditorConfigFix;

internal static class EditorConfigFixCli
{
	public static int Invoke(string[] args, TextWriter outputWriter, TextWriter errorWriter)
	{
		var rootCommand = CreateRootCommand(outputWriter, errorWriter);
		var parseResult = rootCommand.Parse(args);
		if (parseResult.Errors.Count != 0)
		{
			foreach (var parseError in parseResult.Errors)
			{
				errorWriter.WriteLine($"error: {parseError.Message}");
			}

			if (parseResult.Errors.Any(static parseError => parseError.Message.StartsWith("Required argument missing for command", StringComparison.Ordinal)))
			{
				WriteFixOptions(errorWriter);
				rootCommand.Parse("--help").Invoke(new InvocationConfiguration { Output = errorWriter, Error = errorWriter });
			}

			return ExitCodes.CommandLineError;
		}

		return parseResult.Invoke();
	}

	internal static RootCommand CreateRootCommand(TextWriter outputWriter, TextWriter errorWriter)
	{
		var filePathArgument = new Argument<string>("file-path")
		{
			Description = "The file to fix.",
		};

		var anyFileOption = CreateFlag("--any-file", "Allow settings that come only from a matching [*] section.");
		var forceOption = CreateFlag("--force", "Attempt files detected as binary, while still requiring supported UTF-8 decoding before any write.");
		var gitRootOption = CreateFlag("--git-root", "Stop looking for .editorconfig files when a git repository root is reached.");
		var dryRunOption = CreateFlag("--dry-run", "Report whether changes would be made without writing the file.");
		var verifyOption = CreateFlag("--verify", "Return exit code 1 if any change would be made.");
		var endOfLineOption = CreateFlag("--end-of-line", "Apply end_of_line.");
		var stripBomOption = CreateFlag("--strip-bom", "Remove a UTF-8 BOM when charset is utf-8.");
		var trailingWhitespaceOption = CreateFlag("--trailing-whitespace", "Apply trim_trailing_whitespace.");
		var finalNewlineOption = CreateFlag("--final-newline", "Apply insert_final_newline.");

		var rootCommand = new RootCommand("Applies selected EditorConfig settings to one file.");
		rootCommand.Add(filePathArgument);
		rootCommand.Add(anyFileOption);
		rootCommand.Add(forceOption);
		rootCommand.Add(gitRootOption);
		rootCommand.Add(dryRunOption);
		rootCommand.Add(verifyOption);
		rootCommand.Add(endOfLineOption);
		rootCommand.Add(stripBomOption);
		rootCommand.Add(trailingWhitespaceOption);
		rootCommand.Add(finalNewlineOption);
		rootCommand.SetAction(parseResult =>
		{
			var options = new FixOptions(
				parseResult.GetRequiredValue(filePathArgument),
				parseResult.GetValue(anyFileOption),
				parseResult.GetValue(forceOption),
				parseResult.GetValue(gitRootOption),
				parseResult.GetValue(dryRunOption),
				parseResult.GetValue(verifyOption),
				parseResult.GetValue(endOfLineOption),
				parseResult.GetValue(stripBomOption),
				parseResult.GetValue(trailingWhitespaceOption),
				parseResult.GetValue(finalNewlineOption));

			if (!options.HasAnyFix)
			{
				errorWriter.WriteLine("error: at least one fix option must be specified.");
				return ExitCodes.CommandLineError;
			}

			return new EditorConfigFixer(outputWriter, errorWriter).Run(options);
		});

		return rootCommand;
	}

	private static void WriteFixOptions(TextWriter writer)
	{
		writer.WriteLine();
		writer.WriteLine("Fix options:");
		foreach (var fixOption in s_fixOptionNames)
		{
			writer.WriteLine($"  {fixOption}");
		}

		writer.WriteLine();
	}

	private static Option<bool> CreateFlag(string name, string description) => new(name) { Description = description };

	private static readonly string[] s_fixOptionNames =
	[
		"--end-of-line",
		"--strip-bom",
		"--trailing-whitespace",
		"--final-newline",
	];
}