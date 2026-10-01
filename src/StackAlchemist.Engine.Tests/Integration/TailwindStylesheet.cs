using FluentAssertions;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Proof that Tailwind actually ran over a built frontend, shared by the template gates.
///
/// A green <c>next build</c> or <c>vite build</c> says nothing about it. When the PostCSS or
/// Vite wiring is missing, the bundlers pass the stylesheet through untouched and the build
/// still succeeds: V1-DotNet-NextJs once shipped <c>@tailwind base</c> verbatim, and
/// V2-DotNet-NextJs never had a PostCSS config at all, so every utility class in both apps was
/// a no-op. The Tailwind 4 move (#291) changes exactly this wiring, so the gates assert on the
/// emitted CSS: no directive survives, and the utilities the template's own markup uses exist.
/// </summary>
internal static class TailwindStylesheet
{
    private static readonly string[] UncompiledDirectives =
        ["@tailwind", "@import \"tailwindcss\"", "@import 'tailwindcss'", "@config"];

    /// <summary>
    /// Reads every <c>.css</c> file under <paramref name="buildOutputDirectory"/> and returns
    /// them concatenated, after asserting that Tailwind compiled them.
    /// </summary>
    public static string AssertCompiled(string buildOutputDirectory, params string[] expectedSelectors)
    {
        Directory.Exists(buildOutputDirectory).Should().BeTrue(
            $"the build must have produced {buildOutputDirectory}");

        var stylesheets = Directory.GetFiles(buildOutputDirectory, "*.css", SearchOption.AllDirectories);
        stylesheets.Should().NotBeEmpty(
            $"the app imports a Tailwind stylesheet, so the build must emit CSS under {buildOutputDirectory}");

        var css = string.Join('\n', stylesheets.Select(File.ReadAllText));

        foreach (var directive in UncompiledDirectives)
        {
            css.Should().NotContain(directive,
                "a Tailwind directive in the built CSS means Tailwind never ran and every utility "
                + "class in the app is a no-op");
        }

        foreach (var selector in expectedSelectors)
        {
            css.Should().Contain(selector,
                $"the template's own markup uses `{selector.TrimStart('.')}`; its absence means "
                + "Tailwind did not scan the app's source");
        }

        return css;
    }
}
