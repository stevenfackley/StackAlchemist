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
