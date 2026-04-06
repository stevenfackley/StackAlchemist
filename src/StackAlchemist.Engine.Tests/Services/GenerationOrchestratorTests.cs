using System.IO.Abstractions.TestingHelpers;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class GenerationOrchestratorTests
{
    private static (GenerationOrchestrator Sut, ILlmClient Llm, Channel<GenerationContext> Queue, MockFileSystem Fs) BuildSut()
    {
        var fs = new MockFileSystem();
        var queue = Channel.CreateUnbounded<GenerationContext>();

        var templates = Substitute.For<ITemplateProvider>();
        templates.LoadTemplate(Arg.Any<string>()).Returns(new Dictionary<string, string>
        {
            ["dotnet/Program.cs"] = "var builder = WebApplication.CreateBuilder(args);",
        });
        templates.Render(Arg.Any<Dictionary<string, string>>(), Arg.Any<TemplateVariables>()).Returns(new Dictionary<string, string>
        {
            ["dotnet/Program.cs"] = "var builder = WebApplication.CreateBuilder(args);",
        });
        templates.FindInjectionZones(Arg.Any<string>()).Returns([]);
        templates.InjectIntoZone(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(0));

        var reconstruction = Substitute.For<IReconstructionService>();
        reconstruction.Parse(Arg.Any<string>()).Returns(new Dictionary<string, string>
        {
            ["dotnet/Program.cs"] = "var builder = WebApplication.CreateBuilder(args);",
        });
        reconstruction.Reconstruct(Arg.Any<Dictionary<string, string>>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<ITemplateProvider>())
            .Returns(new Dictionary<string, string>
            {
                ["dotnet/Program.cs"] = "var builder = WebApplication.CreateBuilder(args);",
            });

        var llm = Substitute.For<ILlmClient>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("[[FILE:dotnet/Program.cs]]\nvar builder = WebApplication.CreateBuilder(args);\n[[END_FILE]]");

        var promptBuilder = Substitute.For<IPromptBuilderService>();
        promptBuilder.BuildGenerationPrompt(Arg.Any<GenerationSchema>(), Arg.Any<ProjectType>())
            .Returns("Generate code for the provided schema using [[FILE:path]]...[[END_FILE]] format.");

        var sut = new GenerationOrchestrator(
            templates,
            reconstruction,
            llm,
            promptBuilder,
            fs,
            queue.Writer,
            NullLogger<GenerationOrchestrator>.Instance);

        return (sut, llm, queue, fs);
    }

    [Fact]
    public async Task EnqueueAsync_OnSuccess_ReturnsBuildingAndWritesQueue()
    {
        var (sut, _, queue, _) = BuildSut();
        var request = new GenerateRequest
        {
            GenerationId = Guid.NewGuid().ToString(),
            Mode = "simple",
            Tier = 2,
            Prompt = "Build a task manager",
        };

        var response = await sut.EnqueueAsync(request);

        response.JobId.Should().Be(request.GenerationId);
        response.Status.Should().Be("building");
        queue.Reader.TryRead(out var ctx).Should().BeTrue();
        ctx!.State.Should().Be(GenerationState.Building);
        ctx.OutputDirectory.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EnqueueAsync_WhenLlmFails_ReturnsFailedAndDoesNotQueue()
    {
        var (sut, llm, queue, _) = BuildSut();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("upstream failed"));

        var response = await sut.EnqueueAsync(new GenerateRequest
        {
            GenerationId = Guid.NewGuid().ToString(),
            Mode = "simple",
            Tier = 1,
            Prompt = "Build an app",
        });

        response.Status.Should().Be("failed");
        queue.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task EnqueueAsync_WhenTemplateLoadFails_ReturnsFailed()
    {
        var fs = new MockFileSystem();
        var queue = Channel.CreateUnbounded<GenerationContext>();

        var templates = Substitute.For<ITemplateProvider>();
        templates.LoadTemplate(Arg.Any<string>()).Throws(new TemplateNotFoundException("missing template"));

        var reconstruction = Substitute.For<IReconstructionService>();
        var llm = Substitute.For<ILlmClient>();
        var promptBuilder = Substitute.For<IPromptBuilderService>();

        var sut = new GenerationOrchestrator(
            templates,
            reconstruction,
            llm,
            promptBuilder,
            fs,
            queue.Writer,
            NullLogger<GenerationOrchestrator>.Instance);

        var response = await sut.EnqueueAsync(new GenerateRequest
        {
            GenerationId = Guid.NewGuid().ToString(),
            Mode = "advanced",
            Tier = 3,
            Schema = new GenerationSchema(),
        });

        response.Status.Should().Be("failed");
    }
}
