using System.IO.Abstractions.TestingHelpers;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

public class GenerationOrchestratorTests
{
    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    private static IConfiguration ConfigWith(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(entries.ToDictionary(e => e.Key, e => (string?)e.Value))
            .Build();

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
            .Returns(new LlmResponse(
                "[[FILE:dotnet/Program.cs]]\nvar builder = WebApplication.CreateBuilder(args);\n[[END_FILE]]",
                10,
                20,
                "claude-3-5-sonnet-20241022"));

        var promptBuilder = Substitute.For<IPromptBuilderService>();
        promptBuilder.BuildGenerationPrompt(Arg.Any<GenerationSchema>(), Arg.Any<ProjectType>(), Arg.Any<GenerationPersonalization?>())
            .Returns("Generate code for the provided schema using [[FILE:path]]...[[END_FILE]] format.");
        var injectionEngine = Substitute.For<IInjectionEngine>();
        var delivery = Substitute.For<IDeliveryService>();

        var sut = new GenerationOrchestrator(
            templates,
            reconstruction,
            llm,
            promptBuilder,
            injectionEngine,
            delivery,
            fs,
            EmptyConfig(),
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
    public async Task EnqueueAsync_ForTier3_AppendsInfrastructureFiles()
    {
        var fs = new MockFileSystem();
        var queue = Channel.CreateUnbounded<GenerationContext>();

        var templates = Substitute.For<ITemplateProvider>();
        templates.LoadTemplate("V1-DotNet-NextJs").Returns(new Dictionary<string, string>
        {
            ["dotnet/Program.cs"] = "var builder = WebApplication.CreateBuilder(args);",
        });
        templates.LoadTemplate("Tier3-Infrastructure").Returns(new Dictionary<string, string>
        {
            ["DEPLOYMENT.md"] = "# {{ProjectName}}",
            ["infra/helm/Chart.yaml"] = "name: {{ProjectNameKebab}}",
            ["infra/cdk/lib/{{ProjectNameKebab}}-stack.ts"] = "export const stack = '{{ProjectName}}';",
        });
        templates
            .Render(Arg.Any<Dictionary<string, string>>(), Arg.Any<TemplateVariables>())
            .Returns(call =>
            {
                var input = call.ArgAt<Dictionary<string, string>>(0);
                var vars = call.ArgAt<TemplateVariables>(1);

                return input.ToDictionary(
                    pair => pair.Key
                        .Replace("{{ProjectName}}", vars.ProjectName)
                        .Replace("{{ProjectNameKebab}}", vars.ProjectNameKebab),
                    pair => pair.Value
                        .Replace("{{ProjectName}}", vars.ProjectName)
                        .Replace("{{ProjectNameKebab}}", vars.ProjectNameKebab));
            });

        var reconstruction = Substitute.For<IReconstructionService>();
        reconstruction.Parse(Arg.Any<string>()).Returns([]);
        reconstruction
            .Reconstruct(Arg.Any<Dictionary<string, string>>(), Arg.Any<Dictionary<string, string>>(), templates)
            .Returns(new Dictionary<string, string>
            {
                ["dotnet/Program.cs"] = "var builder = WebApplication.CreateBuilder(args);",
            });

        var llm = Substitute.For<ILlmClient>();
        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("", 10, 20, "claude-3-5-sonnet-20241022"));

        var promptBuilder = Substitute.For<IPromptBuilderService>();
        promptBuilder.BuildGenerationPrompt(Arg.Any<GenerationSchema>(), Arg.Any<ProjectType>(), Arg.Any<GenerationPersonalization?>())
            .Returns("Generate code for the provided schema using [[FILE:path]]...[[END_FILE]] format.");
        var injectionEngine = Substitute.For<IInjectionEngine>();
        var delivery = Substitute.For<IDeliveryService>();

        var sut = new GenerationOrchestrator(
            templates,
            reconstruction,
            llm,
            promptBuilder,
            injectionEngine,
            delivery,
            fs,
            EmptyConfig(),
            queue.Writer,
            NullLogger<GenerationOrchestrator>.Instance);

        var response = await sut.EnqueueAsync(new GenerateRequest
        {
            GenerationId = Guid.NewGuid().ToString(),
            Mode = "advanced",
            Tier = 3,
            Schema = new GenerationSchema(),
            Personalization = new GenerationPersonalization
            {
                ProjectName = "Task Manager",
            },
        });

        response.Status.Should().Be("building");
        queue.Reader.TryRead(out var ctx).Should().BeTrue();
        fs.File.Exists(fs.Path.Combine(ctx!.OutputDirectory!, "DEPLOYMENT.md")).Should().BeTrue();
        fs.File.Exists(fs.Path.Combine(ctx.OutputDirectory!, "infra/helm/Chart.yaml")).Should().BeTrue();
        fs.File.Exists(fs.Path.Combine(ctx.OutputDirectory!, "infra/cdk/lib/task-manager-stack.ts")).Should().BeTrue();
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
        var injectionEngine = Substitute.For<IInjectionEngine>();
        var delivery = Substitute.For<IDeliveryService>();

        var sut = new GenerationOrchestrator(
            templates,
            reconstruction,
            llm,
            promptBuilder,
            injectionEngine,
            delivery,
            fs,
            EmptyConfig(),
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

    [Fact]
    public async Task EnqueueAsync_WithSwissCheeseFlag_UsesV2TemplatesAndInjectionEngine()
    {
        var fs = new MockFileSystem();
        var queue = Channel.CreateUnbounded<GenerationContext>();

        var templates = Substitute.For<ITemplateProvider>();
        templates.LoadTemplate("V2-DotNet-NextJs").Returns(new Dictionary<string, string>
        {
            ["dotnet/Repositories/{{EntityName}}Repository.cs"] = "class {{EntityName}}Repository {}",
        });
        templates.Render(Arg.Any<Dictionary<string, string>>(), Arg.Any<TemplateVariables>())
            .Returns(new Dictionary<string, string>
            {
                ["dotnet/Repositories/ProductRepository.cs"] = "class ProductRepository {}",
            });

        var reconstruction = Substitute.For<IReconstructionService>();
        var llm = Substitute.For<ILlmClient>();
        var promptBuilder = Substitute.For<IPromptBuilderService>();
        var delivery = Substitute.For<IDeliveryService>();

        var injectionEngine = Substitute.For<IInjectionEngine>();
        injectionEngine.FillZonesAsync(
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<GenerationSchema>(),
                Arg.Any<TemplateVariables>(),
                Arg.Any<ProjectType>(),
                Arg.Any<GenerationPersonalization?>(),
                Arg.Any<CancellationToken>())
            .Returns(new InjectionResult(
                FilledTemplates: new Dictionary<string, string>
                {
                    ["dotnet/Repositories/ProductRepository.cs"] = "class ProductRepository { /* filled */ }",
                },
                TotalInputTokens: 500,
                TotalOutputTokens: 1200,
                Model: "claude-3-5-sonnet-20241022",
                ZonesFilled: 5));

        var sut = new GenerationOrchestrator(
            templates,
            reconstruction,
            llm,
            promptBuilder,
            injectionEngine,
            delivery,
            fs,
            ConfigWith(("Generation:UseSwissCheese", "true")),
            queue.Writer,
            NullLogger<GenerationOrchestrator>.Instance);

        var response = await sut.EnqueueAsync(new GenerateRequest
        {
            GenerationId = Guid.NewGuid().ToString(),
            Mode = "advanced",
            Tier = 2,
            ProjectType = ProjectType.DotNetNextJs,
            Schema = new GenerationSchema
            {
                Entities = [new SchemaEntity { Name = "Product", Fields = [] }],
            },
        });

        response.Status.Should().Be("building");
        templates.Received(1).LoadTemplate("V2-DotNet-NextJs");
        await injectionEngine.Received(1).FillZonesAsync(
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<GenerationSchema>(),
            Arg.Any<TemplateVariables>(),
            Arg.Any<ProjectType>(),
            Arg.Any<GenerationPersonalization?>(),
            Arg.Any<CancellationToken>());
        await llm.DidNotReceive().GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await delivery.Received(1).UpdateTokenUsageAsync(
            Arg.Any<string>(), 500, 1200, "claude-3-5-sonnet-20241022", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueAsync_DefaultConfig_StillUsesV1OneShot()
    {
        var (sut, llm, queue, _) = BuildSut();

        await sut.EnqueueAsync(new GenerateRequest
        {
            GenerationId = Guid.NewGuid().ToString(),
            Mode = "simple",
            Tier = 2,
            Prompt = "Build a task manager",
        });

        // Default config (no UseSwissCheese set) → V1 path → llmClient.GenerateAsync called.
        await llm.Received(1).GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
