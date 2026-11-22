using FluentAssertions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// The status names a generation row is written with, and the sets of them the reconciler and its
/// sweep query use. The web test <c>generation-status.test.ts</c> checks <see cref="GenerationStatus.All"/>
/// against the database CHECK.
/// </summary>
public sealed class GenerationStatusTests
{
    [Theory]
    [InlineData(GenerationState.Pending, "pending")]
    [InlineData(GenerationState.Generating, "generating")] // not generating_code: the repair loop's name
    [InlineData(GenerationState.Building, "building")]
    [InlineData(GenerationState.Packing, "packing")]
    [InlineData(GenerationState.Uploading, "uploading")]
    [InlineData(GenerationState.Success, "success")]
    [InlineData(GenerationState.Failed, "failed")]
    public void ToWire_IsTheStatusEachStateIsWrittenAs(GenerationState state, string expected) =>
        GenerationStatus.ToWire(state).Should().Be(expected);

    [Fact]
    public void EveryState_IsWrittenAsAStatusTheRowAllows() =>
        Enum.GetValues<GenerationState>().Select(GenerationStatus.ToWire)
            .Should().OnlyContain(status => GenerationStatus.All.Contains(status));

    [Fact]
    public void NonTerminal_IsEveryStatusButSuccessAndFailed()
    {
        GenerationStatus.NonTerminal.Should().Equal(
            "pending", "extracting_schema", "generating_code", "generating", "building", "packing", "uploading");
        GenerationStatus.All.Except(GenerationStatus.NonTerminal).Should().Equal("success", "failed");
    }

    [Fact]
    public void TheSweepQuery_ListsTheNonTerminalStatuses_AsItAlwaysHas() =>
        PostgresDeliveryService.NonTerminalStatuses.Should().Be(
            "('pending', 'extracting_schema', 'generating_code', 'generating', 'building', 'packing', 'uploading')");

    [Fact]
    public void EveryStatusTheReconcilerActsOn_IsOneItsSweepReturns()
    {
        // A status in one of these sets but not in the sweep's would be a row the reconciler
        // means to restart or fail and never sees.
        string[] actedOn =
        [
            .. GenerationReconciliationService.RequeueableStatuses,
            .. GenerationReconciliationService.RestartRequeueableStatuses,
            .. GenerationReconciliationService.InProgressStatuses,
        ];

        actedOn.Should().OnlyContain(status => GenerationStatus.NonTerminal.Contains(status));
    }
}
