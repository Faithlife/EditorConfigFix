namespace EditorConfigFix;

internal static class Program
{
	public static int Main(string[] args) => EditorConfigFixCli.Invoke(args, Console.Out, Console.Error);
}