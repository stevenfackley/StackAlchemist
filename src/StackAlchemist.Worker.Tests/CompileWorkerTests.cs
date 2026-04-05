using FluentAssertions;
using StackAlchemist.Engine.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace StackAlchemist.Worker.Tests;

/// <summary>
/// Unit tests for <see cref="CompileService"/> — error extraction and retry-context building.
/// CompileService has been promoted to StackAlchemist.Engine.Services in Phase 4.
/// </summary>
public class CompileWorkerTests
{
    private readonly CompileService _sut = new(
        [
            new DotNetBuildStrategy(NullLogger<DotNetBuildStrategy>.Instance),
            new PythonReactBuildStrategy(NullLogger<PythonReactBuildStrategy>.Instance),
        ],
        NullLogger<CompileService>.Instance);

    [Fact]
    public void ExtractBuildErrors_FromDotnetOutput_ReturnsParsedErrors()
    {
        var buildOutput = """
            Microsoft (R) Build Engine version 17.0.0
            Build started 1/1/2026 12:00:00 AM.
            src/Controllers/ProductsController.cs(15,22): error CS1002: ; expected
            src/Controllers/ProductsController.cs(20,5): error CS0246: The type or namespace name 'ILogger' could not be found
            Build FAILED.
            """;

        var errors = _sut.ExtractBuildErrors(buildOutput, StackAlchemist.Engine.Models.ProjectType.DotNetNextJs);

        errors.Should().HaveCount(2);
        errors[0].Should().Contain("CS1002");
        errors[1].Should().Contain("CS0246");
    }

    [Fact]
    public void ExtractBuildErrors_NoBuildErrors_ReturnsEmpty()
    {
        var buildOutput = """
            Build succeeded.
                0 Warning(s)
                0 Error(s)
            """;

        var errors = _sut.ExtractBuildErrors(buildOutput, StackAlchemist.Engine.Models.ProjectType.DotNetNextJs);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void BuildRetryContext_IncludesOriginalPromptAndErrors()
    {
        var errorHistory = new List<string>
        {
            "Attempt 1: error CS1002: ; expected",
        };

        var context = _sut.BuildRetryContext("Generate a Product controller", errorHistory, retryAttempt: 1);

        context.Should().Contain("Generate a Product controller");
        context.Should().Contain("CS1002");
        context.Should().Contain("retry attempt 1");
    }

    [Fact]
    public void BuildRetryContext_AccumulatesErrorsAcrossAttempts()
    {
        var errorHistory = new List<string>
        {
            "Attempt 1: error CS1002: ; expected",
            "Attempt 2: error CS0103: The name 'context' does not exist",
        };

        var context = _sut.BuildRetryContext("Generate code", errorHistory, retryAttempt: 3);

        context.Should().Contain("Attempt 1");
        context.Should().Contain("Attempt 2");
        context.Should().Contain("CS1002");
        context.Should().Contain("CS0103");
    }

    [Fact]
    public void BuildRetryContext_TruncatesWhenApproachingLimit()
    {
        var longHistory = Enumerable.Range(1, 100)
            .Select(i => $"Attempt {i}: error CS{i:D4}: " + new string('x', 200))
            .ToList();

        var context = _sut.BuildRetryContext("Generate code", longHistory, retryAttempt: 3);

        // Should be under the token budget (~8000 chars)
        context.Length.Should().BeLessThan(10_000);
        // Should contain the most recent errors, not necessarily the oldest
        context.Should().Contain("Generate code");
    }

    [Fact]
    public void ExtractBuildErrors_FromPythonOutput_ReturnsParsedErrors()
    {
        var buildOutput = """
            backend/app/main.py:10:1: E302 expected 2 blank lines, found 1
            ERROR collecting tests/test_health.py
            """;

        var errors = _sut.ExtractBuildErrors(buildOutput, StackAlchemist.Engine.Models.ProjectType.PythonReact);

        errors.Should().HaveCount(2);
        errors[0].Should().Contain("E302");
        errors[1].Should().Contain("ERROR collecting");
    }
}
