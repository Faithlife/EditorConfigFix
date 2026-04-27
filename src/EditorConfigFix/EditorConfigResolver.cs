using EditorConfig.Core;

namespace EditorConfigFix;

internal sealed class EditorConfigResolver
{
	public ResolvedEditorConfig Resolve(string filePath, bool stopAtGitRoot)
	{
		var fullPath = Path.GetFullPath(filePath);
		var editorConfigFiles = GetEditorConfigFiles(fullPath, stopAtGitRoot);
		var configuration = new EditorConfigParser().Parse(fullPath, editorConfigFiles);
		var matchingSections = editorConfigFiles
			.SelectMany(file => file.Sections)
			.Where(section => IsMatch(section.Glob, fullPath))
			.ToList();

		return new ResolvedEditorConfig(configuration, matchingSections);
	}

	private static List<EditorConfigFile> GetEditorConfigFiles(string fullPath, bool stopAtGitRoot)
	{
		var files = new List<EditorConfigFile>();
		var directory = Path.GetDirectoryName(fullPath);

		while (directory is not null)
		{
			var editorConfigPath = Path.Combine(directory, ".editorconfig");
			if (File.Exists(editorConfigPath))
			{
				var editorConfigFile = EditorConfigFile.Parse(editorConfigPath);
				files.Add(editorConfigFile);
				if (editorConfigFile.IsRoot)
					break;
			}

			if (stopAtGitRoot && IsGitRoot(directory))
				break;

			directory = Directory.GetParent(directory)?.FullName;
		}

		files.Reverse();
		return files;
	}

	private static bool IsGitRoot(string directory) =>
		Directory.Exists(Path.Combine(directory, ".git")) || File.Exists(Path.Combine(directory, ".git"));

	private static bool IsMatch(string glob, string filePath) => GlobMatcher.Create(glob, s_globOptions).IsMatch(filePath);

	private static readonly GlobMatcherOptions s_globOptions = new()
	{
		AllowWindowsPaths = true,
		Dot = true,
		MatchBase = true,
	};
}