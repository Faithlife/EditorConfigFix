using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace EditorConfigFix.Tests;

[TestFixture]
internal sealed class EditorConfigFixCliTests
{
	[SetUp]
	public void SetUp()
	{
		m_temporaryDirectory = Path.Combine(Path.GetTempPath(), "EditorConfigFix.Tests", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
		Directory.CreateDirectory(m_temporaryDirectory);
	}

	[TearDown]
	public void TearDown()
	{
		if (m_temporaryDirectory is not null && Directory.Exists(m_temporaryDirectory))
			Directory.Delete(m_temporaryDirectory, true);
	}

	[Test]
	public void MissingFilePathShowsFixOptionsAndHelp()
	{
		var result = Invoke("--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.CommandLineError));
		Assert.That(result.Output, Is.Empty);
		Assert.That(result.Error, Does.Contain("Required argument missing for command"));
		Assert.That(result.Error, Does.Contain("Fix options:"));
		Assert.That(result.Error, Does.Contain("  --end-of-line"));
		Assert.That(result.Error, Does.Contain("  --strip-bom"));
		Assert.That(result.Error, Does.Contain("  --trailing-whitespace"));
		Assert.That(result.Error, Does.Contain("  --final-newline"));
		Assert.That(result.Error, Does.Contain("Usage:"));
		Assert.That(result.Error.IndexOf("Required argument missing for command", StringComparison.Ordinal), Is.LessThan(result.Error.IndexOf("Fix options:", StringComparison.Ordinal)));
		Assert.That(result.Error.IndexOf("Fix options:", StringComparison.Ordinal), Is.LessThan(result.Error.IndexOf("Usage:", StringComparison.Ordinal)));
	}

	[Test]
	public void NoFixOptionFails()
	{
		var filePath = WriteTextFile("test.txt", "test");

		var result = Invoke(filePath);

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.CommandLineError));
		Assert.That(result.Error, Does.Contain("at least one fix option"));
	}

	[Test]
	public void EndOfLineConvertsMatchingFile()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\nend_of_line = lf\n");
		var filePath = WriteTextFile("test.txt", "one\r\ntwo\r\n");

		var result = Invoke(filePath, "--end-of-line");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(result.Output, Does.Contain("changed"));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one\ntwo\n"));
	}

	[Test]
	public void AnyFileIsRequiredForCatchAllOnlyMatch()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*]\ntrim_trailing_whitespace = true\n");
		var filePath = WriteTextFile("test.txt", "one \n");

		var result = Invoke(filePath, "--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(result.Output, Does.Contain("only [*] matched"));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one \n"));
	}

	[Test]
	public void AnyFileAllowsCatchAllOnlyMatch()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*]\ntrim_trailing_whitespace = true\n");
		var filePath = WriteTextFile("test.txt", "one \n");

		var result = Invoke(filePath, "--any-file", "--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one\n"));
	}

	[Test]
	public void DryRunReportsChangeWithoutWriting()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\ntrim_trailing_whitespace = true\n");
		var filePath = WriteTextFile("test.txt", "one \n");

		var result = Invoke(filePath, "--dry-run", "--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(result.Output, Does.Contain("would change"));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one \n"));
	}

	[Test]
	public void VerifyReturnsOneWhenChangeWouldBeMade()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\ntrim_trailing_whitespace = true\n");
		var filePath = WriteTextFile("test.txt", "one \n");

		var result = Invoke(filePath, "--verify", "--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.VerifyWouldChange));
		Assert.That(result.Output, Does.Contain("would change"));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one \n"));
	}

	[Test]
	public void FinalNewlineDoesNotAddNewlineToEmptyFile()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\ninsert_final_newline = true\n");
		var filePath = WriteTextFile("test.txt", "");
		var lastWriteTime = File.GetLastWriteTimeUtc(filePath);

		var result = Invoke(filePath, "--final-newline");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(result.Output, Does.Contain("unchanged"));
		Assert.That(File.ReadAllText(filePath), Is.Empty);
		Assert.That(File.GetLastWriteTimeUtc(filePath), Is.EqualTo(lastWriteTime));
	}

	[Test]
	public void FinalNewlineFalseDoesNotRemoveExistingNewline()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\ninsert_final_newline = false\n");
		var filePath = WriteTextFile("test.txt", "one\n");

		var result = Invoke(filePath, "--final-newline");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(result.Output, Does.Contain("no selected settings apply"));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one\n"));
	}

	[Test]
	public void StripBomRemovesUtf8BomWhenCharsetIsUtf8()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\ncharset = utf-8\n");
		var filePath = Path.Combine(m_temporaryDirectory!, "test.txt");
		File.WriteAllBytes(filePath, [0xEF, 0xBB, 0xBF, (byte) 'o', (byte) 'n', (byte) 'e']);

		var result = Invoke(filePath, "--strip-bom");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(File.ReadAllBytes(filePath), Is.EqualTo("one"u8.ToArray()));
	}

	[Test]
	public void InvalidUtf8IsSkippedWithoutForce()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.bin]\ntrim_trailing_whitespace = true\n");
		var filePath = Path.Combine(m_temporaryDirectory!, "test.bin");
		File.WriteAllBytes(filePath, [0xFF]);

		var result = Invoke(filePath, "--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(result.Output, Does.Contain("binary file"));
		Assert.That(File.ReadAllBytes(filePath), Is.EqualTo(new byte[] { 0xFF }));
	}

	[Test]
	public void InvalidUtf8FailsWithForce()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.bin]\ntrim_trailing_whitespace = true\n");
		var filePath = Path.Combine(m_temporaryDirectory!, "test.bin");
		File.WriteAllBytes(filePath, [0xFF]);

		var result = Invoke(filePath, "--force", "--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.ProcessingError));
		Assert.That(result.Error, Does.Contain("Unable to translate bytes"));
		Assert.That(File.ReadAllBytes(filePath), Is.EqualTo(new byte[] { 0xFF }));
	}

	[Test]
	public void ValidUtf8WithNullByteCanBeChanged()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\ntrim_trailing_whitespace = true\n");
		var filePath = Path.Combine(m_temporaryDirectory!, "test.txt");
		File.WriteAllBytes(filePath, Encoding.UTF8.GetBytes("one\0 \n"));

		var result = Invoke(filePath, "--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one\0\n"));
	}

	[Test]
	public void UnsupportedCharsetIsSkippedWithoutForceAndFailsWithForce()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\ncharset = utf-16le\ntrim_trailing_whitespace = true\n");
		var filePath = WriteTextFile("test.txt", "one \n");

		var skipped = Invoke(filePath, "--trailing-whitespace");
		var forced = Invoke(filePath, "--force", "--trailing-whitespace");

		Assert.That(skipped.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(skipped.Output, Does.Contain("unsupported charset"));
		Assert.That(forced.ExitCode, Is.EqualTo(ExitCodes.ProcessingError));
		Assert.That(forced.Error, Does.Contain("unsupported charset"));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one \n"));
	}

	[Test]
	public void GitRootStopsParentEditorConfigDiscovery()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\ntrim_trailing_whitespace = true\n");
		var repoDirectory = Path.Combine(m_temporaryDirectory!, "repo");
		Directory.CreateDirectory(Path.Combine(repoDirectory, ".git"));
		var filePath = Path.Combine(repoDirectory, "test.txt");
		File.WriteAllText(filePath, "one \n");

		var result = Invoke(filePath, "--git-root", "--trailing-whitespace");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(result.Output, Does.Contain("no selected settings apply"));
		Assert.That(File.ReadAllText(filePath), Is.EqualTo("one \n"));
	}

	[Test]
	public void AlreadyCompliantFileIsNotTouched()
	{
		WriteTextFile(".editorconfig", "root = true\n\n[*.txt]\nend_of_line = lf\n");
		var filePath = WriteTextFile("test.txt", "one\n");
		var timestamp = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
		File.SetLastWriteTimeUtc(filePath, timestamp);

		var result = Invoke(filePath, "--end-of-line");

		Assert.That(result.ExitCode, Is.EqualTo(ExitCodes.Success));
		Assert.That(result.Output, Does.Contain("unchanged"));
		Assert.That(File.GetLastWriteTimeUtc(filePath), Is.EqualTo(timestamp));
	}

	private CliResult Invoke(params string[] args)
	{
		using var outputWriter = new StringWriter(CultureInfo.InvariantCulture);
		using var errorWriter = new StringWriter(CultureInfo.InvariantCulture);
		var exitCode = EditorConfigFixCli.Invoke(args, outputWriter, errorWriter);
		return new CliResult(exitCode, outputWriter.ToString(), errorWriter.ToString());
	}

	private string WriteTextFile(string relativePath, string text)
	{
		var path = Path.Combine(m_temporaryDirectory!, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, text, new UTF8Encoding(false));
		return path;
	}

	private string? m_temporaryDirectory;
}