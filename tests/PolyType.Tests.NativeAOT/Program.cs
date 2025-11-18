// This file provides a no-op entry point when SkipTUnitTestRuns=true
// to make this project a simple console executable that does nothing,
// preventing MTP from trying to run it as a test project.
// When SkipTUnitTestRuns=true, this is the only source file included in compilation.

internal class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("TUnit tests skipped (SkipTUnitTestRuns=true)");
        return 0;
    }
}
