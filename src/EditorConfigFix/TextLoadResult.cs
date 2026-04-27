namespace EditorConfigFix;

internal sealed record TextLoadResult(SupportedTextFile? File, TextLoadFailureKind FailureKind, string? Message)
{
	public static TextLoadResult Success(SupportedTextFile file) => new(file, TextLoadFailureKind.None, null);

	public static TextLoadResult Failure(TextLoadFailureKind failureKind, string message) => new(null, failureKind, message);
}