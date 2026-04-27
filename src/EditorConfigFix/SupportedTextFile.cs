namespace EditorConfigFix;

internal sealed record SupportedTextFile(string Path, byte[] OriginalBytes, string Text, bool HasUtf8Bom);