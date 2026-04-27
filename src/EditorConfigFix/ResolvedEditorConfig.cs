using EditorConfig.Core;

namespace EditorConfigFix;

internal sealed record ResolvedEditorConfig(FileConfiguration Configuration, IReadOnlyList<ConfigSection> MatchingSections)
{
	public bool HasAnyMatchingSection => MatchingSections.Count != 0;

	public bool HasSpecificMatchingSection => MatchingSections.Any(section => !IsCatchAllSection(section));

	private static bool IsCatchAllSection(ConfigSection section)
	{
		if (section.Glob == "*")
			return true;

		var directory = section.EditorConfigFile.Directory.Replace('\\', '/');
		if (directory.Length != 0 && !directory.EndsWith('/'))
			directory += "/";

		return section.Glob == directory + "**/*";
	}
}