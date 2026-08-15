namespace StackAlchemist.Engine.Services;

/// <summary>
/// One definition of "this path is machine-local build residue, not source".
///
/// Two places need the same answer and must never drift apart:
/// <see cref="TemplateProvider"/> (so a stray <c>obj/</c> in a template tree is never
/// Handlebars-compiled) and <see cref="ProjectArchiver"/> (so <c>node_modules/</c> and
/// <c>.next/</c> produced by the Compile Guarantee build never reach the customer's zip).
/// </summary>
internal static class BuildResiduePaths
{
    private static readonly string[] ExcludedDirectorySegments =
    [
        "obj", "node_modules", ".next", ".git", "__pycache__", ".venv",
    ];

    // "bin" alone is NOT excludable: a CDK app's entrypoint lives at infra/cdk/bin/app.ts.
    // .NET build output is always bin/<Configuration>/, so match that shape instead.
    private static readonly string[] DotNetBuildConfigurations = ["Debug", "Release"];

    /// <summary>
    /// True when a relative, '/'-separated <em>file</em> path sits under a build-output
    /// directory. The final segment is the file name and is never itself examined, so a
    /// source file literally called <c>obj.cs</c> is kept.
    /// </summary>
    public static bool IsResidueFile(string relativeFilePath)
    {
        var segments = relativeFilePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return ContainsResidueSegment(segments, segments.Length - 1);
    }

    /// <summary>
    /// True when a relative, '/'-separated <em>directory</em> path is (or sits under) a
    /// build-output directory. Used to prune a directory walk before descending into it —
    /// a rendered project's <c>node_modules/</c> holds 23k+ files that must not even be
    /// enumerated, let alone zipped.
    /// </summary>
    public static bool IsResidueDirectory(string relativeDirectoryPath)
    {
        var segments = relativeDirectoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return ContainsResidueSegment(segments, segments.Length);
    }

    private static bool ContainsResidueSegment(string[] segments, int directorySegmentCount)
    {
        for (var i = 0; i < directorySegmentCount; i++)
        {
            foreach (var excluded in ExcludedDirectorySegments)
            {
                if (string.Equals(segments[i], excluded, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (!string.Equals(segments[i], "bin", StringComparison.OrdinalIgnoreCase))
                continue;

            // bin/Debug/… or bin/Release/… — .NET output, not a source directory.
            if (i + 1 < directorySegmentCount
                && DotNetBuildConfigurations.Contains(segments[i + 1], StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
