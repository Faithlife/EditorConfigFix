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
		var contentBytes = hasUtf8Bom ? bytes.AsSpan(Utf8Preamble.Length) : bytes.AsSpan();

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
		bytes.Length >= Utf8Preamble.Length && bytes.AsSpan(0, Utf8Preamble.Length).SequenceEqual(Utf8Preamble);

	private static readonly byte[] Utf8Preamble = [0xEF, 0xBB, 0xBF];
	private static readonly UTF8Encoding s_strictUtf8 = new(false, true);
}