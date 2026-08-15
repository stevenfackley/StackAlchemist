using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class CompileServiceTests
{
    private static CompileService BuildSut() => new(
        [
            new DotNetBuildStrategy(NullLogger<DotNetBuildStrategy>.Instance),
            new PythonReactBuildStrategy(NullLogger<PythonReactBuildStrategy>.Instance),
        ],
        NullLogger<CompileService>.Instance);

    [Fact]
    public void ExtractBuildErrors_WithCompilerOutput_ReturnsOnlyErrorLines()
    {
        var sut = BuildSut();
        var output = """
            Program.cs(10,5): error CS1002: ; expected
            Program.cs(11,5): warning CS0168: variable is declared but never used
            Repo.cs(21,8): error CS0246: The type or namespace name 'Foo' could not be found
            """;

        var errors = sut.ExtractBuildErrors(output, ProjectType.DotNetNextJs);

        errors.Should().HaveCount(2);
        errors[0].Should().Contain("error CS1002");
        errors[1].Should().Contain("error CS0246");
    }

    [Fact]
    public void BuildRetryContext_IncludesRetryAttemptErrorsAndOriginalPrompt()
    {
        var sut = BuildSut();
        var originalPrompt = "Generate CRUD for Product and Order.";
        var errorHistory = new List<string>
        {
            "Program.cs(10,5): error CS1002: ; expected",
            "Repo.cs(21,8): error CS0246: type not found",
        };

        var context = sut.BuildRetryContext(originalPrompt, errorHistory, retryAttempt: 2);

        context.Should().Contain("retry attempt 2 of 3");
        context.Should().Contain("error CS1002");
        context.Should().Contain("error CS0246");
        context.Should().Contain("Original Prompt");
        context.Should().Contain(originalPrompt);
    }

    /// <summary>
    /// The one prompt in the pipeline that asked for <c>[[FILE:path]]</c> blocks while stating no
    /// paths at all — and the only one that runs when a paid generation is already failing. Its
    /// "original prompt" is the user's natural-language brief, which names no directories, so a
    /// model answering it had nothing to go on but the build errors. Blocks that miss the tree are
    /// now dropped rather than orphaned, which makes an unstated rule an expensive one: the fix
    /// is discarded, the build fails again identically, and the retry budget drains toward a
    /// Compile Guarantee refund.
    /// </summary>
    [Fact]
    public void BuildRetryContext_WithTreeRoots_StatesWhereCorrectedFilesMayGo()
    {
        var sut = BuildSut();

        var context = sut.BuildRetryContext(
            "Build an invoicing app.", ["error CS0246: type not found"], retryAttempt: 1,
            treeRoots: ["dotnet", "nextjs"]);

        context.Should().Contain("`dotnet/`").And.Contain("`nextjs/`");
        context.Should().Contain("nextjs/src/", "the frontend path is the one the old prompt got wrong");
        context.Should().Contain("__zone__", "zones are resolved by repair time and cannot be filled again");
    }

    [Fact]
    public void BuildRetryContext_WithNoTreeRoots_OmitsThePathRulesRatherThanInventingThem()
    {
        var sut = BuildSut();

        var context = sut.BuildRetryContext(
            "Build an invoicing app.", ["error CS0246: type not found"], retryAttempt: 1);

        context.Should().NotContain("Paths Are Not Negotiable");
        context.Should().Contain("Fix ALL build errors listed above.");
    }

    [Fact]
    public void BuildRetryContext_WhenTheOnlyErrorEntryExceedsTheBudget_TruncatesInsteadOfDroppingIt()
    {
        // A `next build` failure now carries file paths and code frames, so a single
        // attempt's entry can be larger than the whole 8k context budget. The old loop
        // silently included nothing, producing a retry prompt with an empty error section
        // — which teaches the LLM nothing, burns all three attempts, and ends in a
        // Compile Guarantee refund.
        var sut = BuildSut();
        var huge = "./src/app/page.tsx:5:9\nType error: MARKER-START "
                   + new string('x', 20_000) + " MARKER-END";

        var context = sut.BuildRetryContext("Generate CRUD", [huge], retryAttempt: 1);

        context.Should().Contain("MARKER-START", "the retry prompt must never ship an empty error section");
        context.Should().Contain("./src/app/page.tsx:5:9");
        context.Should().Contain("truncated");
        context.Should().NotContain("MARKER-END", "the tail is what gets clipped, not the actionable head");
    }

    [Fact]
    public void ExtractBuildErrors_WithPythonAndEslintOutput_ReturnsOnlyErrorLines()
    {
        var sut = BuildSut();
        var output = """
            app/main.py:10:1: E302 expected 2 blank lines, found 1
            src/App.tsx
              14:7  error  'unusedValue' is assigned a value but never used  @typescript-eslint/no-unused-vars
            src/api.ts:8:2: error TS2322: Type 'number' is not assignable to type 'string'.
            """;

        var errors = sut.ExtractBuildErrors(output, ProjectType.PythonReact);

        errors.Should().HaveCount(3);
        errors[0].Should().Contain("E302");
        errors[1].Should().Contain("error");
        errors[2].Should().Contain("TS2322");
    }
}
