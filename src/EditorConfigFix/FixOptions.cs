namespace EditorConfigFix;

internal sealed record FixOptions(
	string FilePath,
	bool AnyFile,
	bool GitRoot,
	bool DryRun,
	bool Verify,
	bool EndOfLine,
	bool StripBom,
	bool TrailingWhitespace,
	bool FinalNewline)
{
	public bool HasAnyFix => EndOfLine || StripBom || TrailingWhitespace || FinalNewline;
}