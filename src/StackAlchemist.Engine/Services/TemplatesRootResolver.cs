namespace StackAlchemist.Engine.Services;

/// <summary>
/// Resolves the on-disk root holding the Handlebars template sets. The templates are a
/// runtime asset — they are NOT part of the published DLL output — so their location
/// differs between a published container (copied next to the entrypoint, see Dockerfile)
/// and the dev bin layout (four levels up under <c>src/</c>). An explicit config value
/// (<c>Templates:Root</c> / env <c>Templates__Root</c>) always wins so a deployment can
/// override the location without a code change.
/// </summary>
public static class TemplatesRootResolver
{
    public const string DirectoryName = "StackAlchemist.Templates";

    public static string Resolve(
        string baseDirectory,
        string currentDirectory,
        string? configuredRoot,
        Func<string, bool> directoryExists)
    {
        // Explicit override wins outright — if it is wrong, the downstream "not found"
        // error names exactly the path the operator asked for.
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            return Path.GetFullPath(configuredRoot);

        // Ordered so the container layout (templates copied beside the published DLLs at
        // <BaseDirectory>) is tried before the dev-only bin-relative and project-root
        // fallbacks. The historical resolution only had the last two, which is why a
        // published container resolved to a nonexistent path and reported "Available: none".
        string[] candidates =
        [
            Path.Combine(baseDirectory, DirectoryName),
            Path.Combine(baseDirectory, "..", "..", "..", "..", DirectoryName),
            Path.Combine(currentDirectory, "..", DirectoryName),
            Path.Combine(currentDirectory, DirectoryName),
        ];

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (directoryExists(full))
                return full;
        }

        // Nothing found: return the container default so the "not found" error points at
        // the path the deployment is expected to provide.
        return Path.GetFullPath(Path.Combine(baseDirectory, DirectoryName));
    }
}
