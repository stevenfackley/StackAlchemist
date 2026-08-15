using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// Unit coverage for <see cref="DotNetBuildStrategy.ExtractBuildErrors"/> — the repair
/// loop only gets useful retry context if this regex actually captures the error
/// families a failed `dotnet restore`/`dotnet build` emits.
/// </summary>
public sealed class DotNetBuildStrategyTests
{
    private readonly DotNetBuildStrategy _sut = new(NullLogger<DotNetBuildStrategy>.Instance);

    [Fact]
    public void ExtractBuildErrors_NetSdk1004RestoreFailure_IsCaptured()
    {
        // Exact shape `dotnet build --no-restore` emits when a project was never
        // restored — this was silently swallowed before (regex only matched `CS\d+`),
        // so the LLM repair loop burned all 3 retries with empty error context.
        var output = """
            Build started 1/1/2026 12:00:00 AM.
            /tmp/build/Test.csproj : error NETSDK1004: Assets file '/tmp/build/obj/project.assets.json' not found. Run a NuGet package restore to generate this file.
            Build FAILED.
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("NETSDK1004");
    }

    [Fact]
    public void ExtractBuildErrors_NuGetAndMsBuildErrors_AreCaptured()
    {
        var output = """
            Determining projects to restore...
            /tmp/build/Test.csproj : error NU1101: Unable to find package Foo. No packages exist with this id in source(s): nuget.org
            MSBUILD : error MSB1009: Project file does not exist.
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().HaveCount(2);
        errors.Should().Contain(e => e.Contains("NU1101"));
        errors.Should().Contain(e => e.Contains("MSB1009"));
    }

    [Fact]
    public void ExtractBuildErrors_StillCapturesCSharpCompilerErrors()
    {
        var output = """
            Program.cs(10,5): error CS1002: ; expected
            Program.cs(11,5): warning CS0168: variable is declared but never used
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("CS1002");
    }

    [Fact]
    public void ExtractBuildErrors_NoErrors_ReturnsEmpty()
    {
        var output = """
            Build succeeded.
                0 Warning(s)
                0 Error(s)
            """;

        _sut.ExtractBuildErrors(output).Should().BeEmpty();
    }

    // ── Frontend half ─────────────────────────────────────────────────────────
    //
    // The repair loop only ever sees what comes out of here. A frontend error with no
    // file and no line is unfixable, so three retries burn and the Compile Guarantee
    // refunds a paying customer — on exactly the failures the nextjs/ build leg newly
    // surfaces. Every assertion below is about that.

    [Fact]
    public void ExtractBuildErrors_NextTypeError_CarriesTheFileLineAndCodeFrame()
    {
        // Verbatim `next build` shape: the location is on the line ABOVE the message.
        var output = """
            Failed to compile.

            ./src/app/page.tsx:5:9
            Type error: Type 'string' is not assignable to type 'number'.

              3 | export default function Home() {
              4 |   // totals come from the API
            > 5 |   const total: number = "12";
                |         ^
              6 |   return <main>{total}</main>;

            Next.js build worker exited with code: 1
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("./src/app/page.tsx:5:9", "the LLM cannot patch a file it was never told about");
        errors[0].Should().Contain("Type error: Type 'string' is not assignable to type 'number'.");
        errors[0].Should().Contain("const total: number = \"12\";", "the code frame is what makes the fix unambiguous");
    }

    [Fact]
    public void ExtractBuildErrors_ModuleNotFound_CarriesTheImportingFile()
    {
        var output = """
            Failed to compile.

            ./src/app/layout.tsx
            Module not found: Can't resolve '@/components/nav'
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("./src/app/layout.tsx");
        errors[0].Should().Contain("Module not found: Can't resolve '@/components/nav'");
    }

    [Fact]
    public void ExtractBuildErrors_EslintGroup_AttachesTheFileHeaderToEveryErrorUnderIt()
    {
        // ESLint prints one file header then several errors beneath it.
        var output = """
            ./src/app/page.tsx
              3:10  error  'unused' is assigned a value but never used  @typescript-eslint/no-unused-vars
              7:1   error  Unexpected console statement                 no-console
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().HaveCount(2);
        errors.Should().OnlyContain(e => e.Contains("./src/app/page.tsx", StringComparison.Ordinal));
        errors.Should().Contain(e => e.Contains("no-unused-vars", StringComparison.Ordinal));
        errors.Should().Contain(e => e.Contains("no-console", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractBuildErrors_DoesNotAttachAnUnrelatedPathToAMessage()
    {
        // Nothing path-shaped directly above the failure — better no location than a wrong one.
        var output = """
            ./src/app/page.tsx
            info  - Compiled successfully
            Type error: Cannot find name 'foo'.
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().ContainSingle();
        errors[0].Should().NotContain("./src/app/page.tsx");
        errors[0].Should().Contain("Cannot find name 'foo'.");
    }

    [Fact]
    public void ExtractBuildErrors_SameMessageInBothStdoutAndStderr_CollapsesToTheRicherBlock()
    {
        // DotNetBuildStrategy.Fail keeps the transcript AND the failing step's stderr, and
        // CompileWorkerService feeds both in. The customer-visible error list must not
        // double every frontend failure.
        var output = """
            ./src/app/page.tsx:5:9
            Type error: Type 'string' is not assignable to type 'number'.

            > 5 |   const total: number = "12";

            --- stderr ---
            Type error: Type 'string' is not assignable to type 'number'.
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("./src/app/page.tsx:5:9", "the copy with context is the one worth keeping");
    }

    [Fact]
    public void ExtractBuildErrors_MixedDotNetAndFrontendFailures_ReturnsBoth()
    {
        var output = """
            $ dotnet build --no-restore  (exit 1)
            /src/dotnet/InvoiceHub.csproj : error CS1002: ; expected

            $ npm run build  (exit 1)
            ./src/app/page.tsx:5:9
            Type error: Cannot find name 'foo'.
            """;

        var errors = _sut.ExtractBuildErrors(output);

        errors.Should().HaveCount(2);
        errors.Should().Contain(e => e.Contains("CS1002", StringComparison.Ordinal));
        errors.Should().Contain(e => e.Contains("./src/app/page.tsx:5:9", StringComparison.Ordinal));
    }
}
