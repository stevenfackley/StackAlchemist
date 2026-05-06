using System.IO.Abstractions.TestingHelpers;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

public class MultiEcosystemPipelineTests
{
    [Theory]
    [InlineData(ProjectType.DotNetNextJs, "V1-DotNet-NextJs", "dotnet/Program.cs")]
    [InlineData(ProjectType.PythonReact, "V1-Python-React", "backend/app/main.py")]
    public async Task EnqueueAsync_UsesExpectedTemplateSetForProjectType(
        ProjectType projectType,
        string expectedTemplateSet,
        string outputPath)
    {
        var fs = new MockFileSystem();
        var queue = Channel.CreateUnbounded<GenerationContext>();
        var templates = Substitute.For<ITemplateProvider>();
        var reconstruction = Substitute.For<IReconstructionService>();
        var llm = Substitute.For<ILlmClient>();
        var promptBuilder = Substitute.For<IPromptBuilderService>();
        var delivery = Substitute.For<IDeliveryService>();

        templates.LoadTemplate(expectedTemplateSet).Returns(new Dictionary<string, string>
        {
            [outputPath] = "template",
        });
        templates.Render(Arg.Any<Dictionary<string, string>>(), Arg.Any<TemplateVariables>()).Returns(new Dictionary<string, string>
        {
            [outputPath] = "rendered",
        });
        templates.FindInjectionZones(Arg.Any<string>()).Returns([]);
        templates.InjectIntoZone(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(0));

        llm.GenerateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse(
                $"[[FILE:{outputPath}]]\nrendered\n[[END_FILE]]",
                10,
                20,
                "claude-3-5-sonnet-20241022"));
        promptBuilder.BuildGenerationPrompt(Arg.Any<GenerationSchema>(), projectType, Arg.Any<GenerationPersonalization?>())
            .Returns($"Prompt for {projectType}");
        reconstruction.Parse(Arg.Any<string>()).Returns(new Dictionary<string, string>
        {
            [outputPath] = "rendered",
        });
        reconstruction.Reconstruct(
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<Dictionary<string, string>>(),
            templates).Returns(new Dictionary<string, string>
            {
                [outputPath] = "rendered",
            });

        var injectionEngine = Substitute.For<IInjectionEngine>();
        var sut = new GenerationOrchestrator(
            templates,
            reconstruction,
            llm,
            promptBuilder,
            injectionEngine,
            delivery,
            new TierGatingService(),
            fs,
            new ConfigurationBuilder().Build(),
            queue.Writer,
            NullLogger<GenerationOrchestrator>.Instance);

        var response = await sut.EnqueueAsync(new GenerateRequest
        {
            GenerationId = Guid.NewGuid().ToString(),
            Mode = "advanced",
            Tier = 2,
            ProjectType = projectType,
            Schema = new GenerationSchema
            {
                Entities =
                [
                    new SchemaEntity
                    {
                        Name = "Product",
                        Fields = [new SchemaField { Name = "id", Type = "uuid", Pk = true }],
                    },
                ],
            },
        });

        response.ProjectType.Should().Be(projectType);
        response.Status.Should().Be("building");
        templates.Received(1).LoadTemplate(expectedTemplateSet);
        promptBuilder.Received(1).BuildGenerationPrompt(Arg.Any<GenerationSchema>(), projectType, Arg.Any<GenerationPersonalization?>());
        queue.Reader.TryRead(out var context).Should().BeTrue();
        context!.ProjectType.Should().Be(projectType);
    }
}
