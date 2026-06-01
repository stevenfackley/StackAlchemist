using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class TemplatesRootResolverTests
{
    // Absolute, cross-platform base/current dirs with enough depth that the dev
    // "four levels up" candidate resolves to a distinct path.
    private static readonly string BaseDir =
        Path.Combine(Path.GetTempPath(), "engine", "bin", "Release", "net10.0");
    private static readonly string CurrentDir =
        Path.Combine(Path.GetTempPath(), "engine");

    private static string Full(params string[] parts) => Path.GetFullPath(Path.Combine(parts));

    private static Func<string, bool> Exists(params string[] fullPaths)
    {
        var set = new HashSet<string>(fullPaths, StringComparer.Ordinal);
        return set.Contains;
    }

    [Fact]
    public void Resolve_PrefersContainerLayout_WhenTemplatesSitBesideTheEntrypoint()
    {
        var container = Full(BaseDir, TemplatesRootResolver.DirectoryName);

        var result = TemplatesRootResolver.Resolve(
            BaseDir, CurrentDir, configuredRoot: null, directoryExists: Exists(container));

        // This is the case the old resolution missed — a published container has the
        // templates next to the DLL, not four levels up.
        result.Should().Be(container);
    }

    [Fact]
    public void Resolve_ConfiguredRoot_WinsOutright_EvenIfItDoesNotExist()
    {
        var configured = Path.Combine(Path.GetTempPath(), "custom-templates");

        var result = TemplatesRootResolver.Resolve(
            BaseDir, CurrentDir, configuredRoot: configured, directoryExists: Exists());

        result.Should().Be(Path.GetFullPath(configured));
    }

    [Fact]
    public void Resolve_FallsBackToDevBinLayout_WhenContainerPathMissing()
    {
        var devBin = Full(BaseDir, "..", "..", "..", "..", TemplatesRootResolver.DirectoryName);

        var result = TemplatesRootResolver.Resolve(
            BaseDir, CurrentDir, configuredRoot: null, directoryExists: Exists(devBin));

        result.Should().Be(devBin);
    }

    [Fact]
    public void Resolve_ContainerLayout_TakesPrecedenceOverDevBinLayout()
    {
        var container = Full(BaseDir, TemplatesRootResolver.DirectoryName);
        var devBin = Full(BaseDir, "..", "..", "..", "..", TemplatesRootResolver.DirectoryName);

        var result = TemplatesRootResolver.Resolve(
            BaseDir, CurrentDir, configuredRoot: null, directoryExists: Exists(container, devBin));

        result.Should().Be(container);
    }

    [Fact]
    public void Resolve_ReturnsContainerDefault_WhenNothingExists_SoTheErrorNamesASanePath()
    {
        var container = Full(BaseDir, TemplatesRootResolver.DirectoryName);

        var result = TemplatesRootResolver.Resolve(
            BaseDir, CurrentDir, configuredRoot: null, directoryExists: Exists());

        result.Should().Be(container);
    }
}
