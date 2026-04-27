namespace EditorConfigFix;

internal sealed class FileWriter
{
	public void WriteBytes(string path, byte[] bytes)
	{
		var directory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory;
		var fileName = Path.GetFileName(path);
		var temporaryPath = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
		var attributes = File.GetAttributes(path);

		try
		{
			File.WriteAllBytes(temporaryPath, bytes);
			File.SetAttributes(temporaryPath, attributes);
			File.Move(temporaryPath, path, true);
			File.SetAttributes(path, attributes);
		}
		finally
		{
			if (File.Exists(temporaryPath))
				File.Delete(temporaryPath);
		}
	}
}