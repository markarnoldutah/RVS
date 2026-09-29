using FluentAssertions;
using RVS.Blazor.Intake.State;
using RVS.Domain.DTOs;
using RVS.UI.Shared.Tests.Fakes;

namespace RVS.UI.Shared.Tests.State;

/// <summary>
/// Tests for several issues per visit in <see cref="IntakeWizardState"/> (<c>Spec A-17</c>,
/// issue #806). Contact and vehicle are entered once; Steps 5–7 are answered once per issue.
/// The step components keep binding to the state's issue properties, which always hold the
/// active issue; the others wait in <see cref="IntakeWizardState.GetIssues"/>.
/// </summary>
public class IntakeWizardStateMultiIssueTests
{
    private static IntakeWizardState CreateState() => new(new NullJSRuntime());

    private static IntakeWizardState StateWithFirstIssue()
    {
        var state = CreateState();
        state.FirstName = "Jane";
        state.LastName = "Doe";
        state.Email = "jane@example.com";
        state.IssueCategory = "Slides";
        state.IssueDescription = "Slide will not retract";
        state.Urgency = "Soon";
        state.RvUsage = "Full-time";
        state.DiagnosticResponses = [new DiagnosticResponseDto { QuestionText = "Does it hum?" }];
        state.Attachments = [new AttachmentFileInfo { FileName = "slide.jpg", FileData = [1] }];
        return state;
    }

    [Fact]
    public void NewState_ShouldHoldOneIssue()
    {
        var state = CreateState();

        state.IssueCount.Should().Be(1);
        state.ActiveIssueIndex.Should().Be(0);
        state.CanAddIssue.Should().BeTrue();
    }

    [Fact]
    public async Task StartNewIssueAsync_ShouldKeepTheFirstIssueAndOpenABlankOneOnStep5()
    {
        var state = StateWithFirstIssue();
        await state.GoToStepAsync(8);

        await state.StartNewIssueAsync();

        state.IssueCount.Should().Be(2);
        state.ActiveIssueIndex.Should().Be(1);
        state.CurrentStep.Should().Be(5);
        state.IssueCategory.Should().BeEmpty();
        state.IssueDescription.Should().BeEmpty();
        state.Urgency.Should().BeNull();
        state.DiagnosticResponses.Should().BeEmpty();
        state.DiagnosticQuestions.Should().BeEmpty();
        state.Attachments.Should().BeEmpty();
        state.CapabilityAssessment.Should().BeNull();
        state.GetIssues()[0].IssueDescription.Should().Be("Slide will not retract");
    }

    [Fact]
    public async Task StartNewIssueAsync_ShouldCarryTheVisitWideAnswersForward()
    {
        var state = StateWithFirstIssue();

        await state.StartNewIssueAsync();

        // RV usage describes the visit, not the issue: asked once, carried forward, editable.
        state.RvUsage.Should().Be("Full-time");
        state.FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task StartNewIssueAsync_AtTenIssues_ShouldDoNothing()
    {
        var state = StateWithFirstIssue();
        for (var i = 1; i < 10; i++)
        {
            await state.StartNewIssueAsync();
            state.IssueDescription = $"Issue {i + 1}";
        }

        state.CanAddIssue.Should().BeFalse();
        await state.StartNewIssueAsync();

        state.IssueCount.Should().Be(10);
        state.IssueDescription.Should().Be("Issue 10");
    }

    [Fact]
    public async Task GetIssues_ShouldReflectEditsToTheActiveIssue()
    {
        var state = StateWithFirstIssue();
        await state.StartNewIssueAsync();

        state.IssueDescription = "Fridge is warm";

        state.GetIssues()[1].IssueDescription.Should().Be("Fridge is warm");
    }

    [Fact]
    public async Task EditIssueAsync_ShouldLoadThatIssueAndComeBackToReview()
    {
        var state = StateWithFirstIssue();
        await state.StartNewIssueAsync();
        state.IssueCategory = "Appliances";
        state.IssueDescription = "Fridge is warm";

        await state.EditIssueAsync(0, 5);

        state.ActiveIssueIndex.Should().Be(0);
        state.CurrentStep.Should().Be(5);
        state.IssueDescription.Should().Be("Slide will not retract");
        state.Attachments.Should().ContainSingle(a => a.FileName == "slide.jpg");
        state.ReturnToStepAfterEdit.Should().Be(8);
        state.GetIssues()[1].IssueDescription.Should().Be("Fridge is warm");
    }

    [Fact]
    public async Task RemoveIssueAsync_ShouldDropThatIssueAndKeepTheRest()
    {
        var state = StateWithFirstIssue();
        await state.StartNewIssueAsync();
        state.IssueDescription = "Fridge is warm";
        await state.StartNewIssueAsync();
        state.IssueDescription = "Awning torn";

        await state.RemoveIssueAsync(1);

        state.GetIssues().Select(i => i.IssueDescription).Should().Equal("Slide will not retract", "Awning torn");
        state.IssueDescription.Should().Be(state.GetIssues()[state.ActiveIssueIndex].IssueDescription);
    }

    [Fact]
    public async Task RemoveIssueAsync_ShouldNeverRemoveTheOnlyIssue()
    {
        var state = StateWithFirstIssue();

        await state.RemoveIssueAsync(0);

        state.IssueCount.Should().Be(1);
        state.IssueDescription.Should().Be("Slide will not retract");
    }

    [Fact]
    public async Task PruneEmptyIssues_ShouldDropABlankIssueTheCustomerBackedOutOf()
    {
        var state = StateWithFirstIssue();
        await state.StartNewIssueAsync();

        state.PruneEmptyIssues();

        state.IssueCount.Should().Be(1);
        state.ActiveIssueIndex.Should().Be(0);
        state.IssueDescription.Should().Be("Slide will not retract");
    }

    [Fact]
    public void PruneEmptyIssues_ShouldKeepTheFirstIssueEvenWhenBlank()
    {
        var state = CreateState();

        state.PruneEmptyIssues();

        state.IssueCount.Should().Be(1);
    }

    [Fact]
    public async Task ValidateAllIssues_ShouldNameEveryIncompleteIssue()
    {
        var state = StateWithFirstIssue();
        await state.StartNewIssueAsync();
        state.IssueDescription = "Fridge is warm";

        var errors = state.ValidateAllIssues();

        errors.Should().ContainSingle().Which.Should().Contain("Issue 2");
    }

    [Fact]
    public void BuildCreateRequest_WithOneIssue_ShouldSendNoAdditionalIssues()
    {
        var request = StateWithFirstIssue().BuildCreateRequest();

        request.IssueDescription.Should().Be("Slide will not retract");
        request.AdditionalIssues.Should().BeNull();
        request.ExpectedAttachmentCount.Should().Be(1);
    }

    [Fact]
    public async Task BuildCreateRequest_WithSeveralIssues_ShouldSendTheFirstInlineAndTheRestInOrder()
    {
        var state = StateWithFirstIssue();
        await state.StartNewIssueAsync();
        state.IssueCategory = "Appliances";
        state.IssueDescription = "Fridge is warm";
        state.Urgency = "Today";
        state.Attachments =
        [
            new AttachmentFileInfo { FileName = "a.jpg", FileData = [1] },
            new AttachmentFileInfo { FileName = "b.jpg", FileData = [1] },
        ];
        state.CapabilityAssessment = new CapabilityAssessmentResponseDto { Matched = false, MissingCapabilities = ["appliance-repair"] };

        var request = state.BuildCreateRequest();

        request.IssueCategory.Should().Be("Slides");
        request.IssueDescription.Should().Be("Slide will not retract");
        request.Urgency.Should().Be("Soon");
        request.ExpectedAttachmentCount.Should().Be(1);
        request.CapabilityMismatchNote.Should().BeNull();
        request.RvUsage.Should().Be("Full-time");
        var fridge = request.AdditionalIssues.Should().ContainSingle().Subject;
        fridge.IssueCategory.Should().Be("Appliances");
        fridge.IssueDescription.Should().Be("Fridge is warm");
        fridge.Urgency.Should().Be("Today");
        fridge.ExpectedAttachmentCount.Should().Be(2);
        fridge.CapabilityMismatchNote.Should().Contain("appliance-repair");
    }

    [Fact]
    public async Task PersistAndRestore_ShouldKeepEveryIssueAndWhichOneIsOpen()
    {
        var jsRuntime = new InMemoryWebStorageJSRuntime();
        var before = new IntakeWizardState(jsRuntime) { Slug = "test-slug" };
        before.IssueCategory = "Slides";
        before.IssueDescription = "Slide will not retract";
        await before.StartNewIssueAsync();
        before.IssueDescription = "Fridge is warm";
        await before.PersistAsync();

        var after = new IntakeWizardState(jsRuntime);
        await after.RestoreAsync();

        after.IssueCount.Should().Be(2);
        after.ActiveIssueIndex.Should().Be(1);
        after.IssueDescription.Should().Be("Fridge is warm");
        after.GetIssues()[0].IssueDescription.Should().Be("Slide will not retract");
    }

    [Fact]
    public async Task ClearAsync_ShouldReturnToASingleBlankIssue()
    {
        var state = StateWithFirstIssue();
        await state.StartNewIssueAsync();

        await state.ClearAsync();

        state.IssueCount.Should().Be(1);
        state.ActiveIssueIndex.Should().Be(0);
        state.IssueDescription.Should().BeEmpty();
    }
}
