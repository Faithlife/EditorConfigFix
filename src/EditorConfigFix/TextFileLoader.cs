using System.Text;
using EditorConfig.Core;

namespace EditorConfigFix;

internal sealed class TextFileLoader
{
	public TextLoadResult Load(string path, FileConfiguration configuration)
	{
		if (configuration.Charset is { } charset && charset is not (Charset.UTF8 or Charset.UTF8BOM))
			return TextLoadResult.Failure(TextLoadFailureKind.UnsupportedCharset, $"unsupported charset: {charset}");

		var bytes = File.ReadAllBytes(path);
		var hasUtf8Bom = HasUtf8Bom(bytes);
		var contentBytes = hasUtf8Bom ? bytes.AsSpan(s_utf8Preamble.Length) : bytes.AsSpan();

		try
		{
			var text = s_strictUtf8.GetString(contentBytes);
			return TextLoadResult.Success(new SupportedTextFile(path, bytes, text, hasUtf8Bom));
		}
		catch (DecoderFallbackException ex)
		{
			return TextLoadResult.Failure(TextLoadFailureKind.Binary, ex.Message);
		}
	}

	private static bool HasUtf8Bom(byte[] bytes) =>
		bytes.Length >= s_utf8Preamble.Length && bytes.AsSpan(0, s_utf8Preamble.Length).SequenceEqual(s_utf8Preamble);

	private static readonly byte[] s_utf8Preamble = [0xEF, 0xBB, 0xBF];
	private static readonly UTF8Encoding s_strictUtf8 = new(false, true);
}