using System.Text;
using EditorConfig.Core;

namespace EditorConfigFix;

internal sealed class EditorConfigFixer
{
	public EditorConfigFixer(TextWriter outputWriter, TextWriter errorWriter)
	{
		_outputWriter = outputWriter;
		_errorWriter = errorWriter;
	}

	public int Run(FixOptions options)
	{
		try
		{
			return RunCore(options);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or InvalidOperationException)
		{
			_errorWriter.WriteLine($"error: {ex.Message}");
			return ExitCodes.ProcessingError;
		}
	}

	private int RunCore(FixOptions options)
	{
		var fullPath = Path.GetFullPath(options.FilePath);
		if (!File.Exists(fullPath))
		{
			_errorWriter.WriteLine($"error: file not found: {options.FilePath}");
			return ExitCodes.CommandLineError;
		}

		if (Directory.Exists(fullPath))
		{
			_errorWriter.WriteLine($"error: path is a directory: {options.FilePath}");
			return ExitCodes.CommandLineError;
		}

		var resolvedEditorConfig = _editorConfigResolver.Resolve(fullPath, options.GitRoot);
		if (!options.AnyFile && resolvedEditorConfig.HasAnyMatchingSection && !resolvedEditorConfig.HasSpecificMatchingSection)
		{
			_outputWriter.WriteLine($"skipped {fullPath}: only [*] matched");
			return ExitCodes.Success;
		}

		if (!HasApplicableSelectedSetting(options, resolvedEditorConfig.Configuration))
		{
			_outputWriter.WriteLine($"skipped {fullPath}: no selected settings apply");
			return ExitCodes.Success;
		}

		var loadResult = _textFileLoader.Load(fullPath, resolvedEditorConfig.Configuration);
		if (loadResult.File is null)
			return HandleLoadFailure(options, fullPath, loadResult);

		var candidateBytes = GetCandidateBytes(loadResult.File, resolvedEditorConfig.Configuration, options);
		if (loadResult.File.OriginalBytes.SequenceEqual(candidateBytes))
		{
			_outputWriter.WriteLine($"unchanged {fullPath}");
			return ExitCodes.Success;
		}

		if (options.DryRun || options.Verify)
		{
			_outputWriter.WriteLine($"would change {fullPath}");
			return options.Verify ? ExitCodes.VerifyWouldChange : ExitCodes.Success;
		}

		_fileWriter.WriteBytes(fullPath, candidateBytes);
		_outputWriter.WriteLine($"changed {fullPath}");
		return ExitCodes.Success;
	}

	private int HandleLoadFailure(FixOptions options, string fullPath, TextLoadResult loadResult)
	{
		if (!options.Force)
		{
			var reason = loadResult.FailureKind == TextLoadFailureKind.UnsupportedCharset ? "unsupported charset" : "binary file";
			_outputWriter.WriteLine($"skipped {fullPath}: {reason}");
			return ExitCodes.Success;
		}

		_errorWriter.WriteLine($"error: {loadResult.Message}");
		return ExitCodes.ProcessingError;
	}

	private static bool HasApplicableSelectedSetting(FixOptions options, FileConfiguration configuration) =>
		(options.EndOfLine && configuration.EndOfLine.HasValue) ||
		(options.StripBom && configuration.Charset == Charset.UTF8) ||
		(options.TrailingWhitespace && configuration.TrimTrailingWhitespace == true) ||
		(options.FinalNewline && configuration.InsertFinalNewline == true);

	private static byte[] GetCandidateBytes(SupportedTextFile textFile, FileConfiguration configuration, FixOptions options)
	{
		var text = textFile.Text;
		if (options.EndOfLine && configuration.EndOfLine is { } endOfLine)
			text = TextTransforms.NormalizeLineEndings(text, endOfLine);

		if (options.TrailingWhitespace && configuration.TrimTrailingWhitespace == true)
			text = TextTransforms.TrimTrailingWhitespace(text);

		if (options.FinalNewline && configuration.InsertFinalNewline == true)
			text = TextTransforms.ApplyFinalNewline(text, configuration.EndOfLine);

		var textBytes = Encoding.UTF8.GetBytes(text);
		var includeBom = textFile.HasUtf8Bom && !(options.StripBom && configuration.Charset == Charset.UTF8);
		if (!includeBom)
			return textBytes;

		var result = new byte[Utf8Preamble.Length + textBytes.Length];
		Utf8Preamble.CopyTo(result, 0);
		textBytes.CopyTo(result, Utf8Preamble.Length);
		return result;
	}

	private static readonly byte[] Utf8Preamble = [0xEF, 0xBB, 0xBF];
	private readonly EditorConfigResolver _editorConfigResolver = new();
	private readonly TextFileLoader _textFileLoader = new();
	private readonly FileWriter _fileWriter = new();
	private readonly TextWriter _outputWriter;
	private readonly TextWriter _errorWriter;
}