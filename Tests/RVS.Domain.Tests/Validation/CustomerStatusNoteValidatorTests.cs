using FluentAssertions;
using RVS.Domain.Validation;

namespace RVS.Domain.Tests.Validation;

/// <summary>
/// Tests for <see cref="CustomerStatusNoteValidator"/> (<c>Spec C-9</c>): the manager-authored
/// note that renders on the customer status page. Blank input is valid and means "clear the note".
/// </summary>
public class CustomerStatusNoteValidatorTests
{
    [Theory]
    [InlineData("waiting on a back-ordered slide motor, ETA Friday")]
    [InlineData("Parts arrived — tech starts Monday.")]
    [InlineData("Line one.\nLine two.")]
    [InlineData("Cost estimate is $1,250 (incl. tax).")]
    public void Validate_CleanNote_ReturnsSuccess(string note)
    {
        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankNote_ReturnsSuccess_MeaningClear(string? note)
    {
        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData("angle < bracket")]
    [InlineData("angle > bracket")]
    [InlineData("semi; colon")]
    [InlineData("it's broken")]
    [InlineData("say \"hi\"")]
    [InlineData("back\\slash")]
    [InlineData("null\0char")]
    public void Validate_BlockedCharacter_ReturnsFailure(string note)
    {
        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ErrorMessage.Should().Contain("blocked character");
    }

    [Fact]
    public void Validate_ExactlyMaxLength_ReturnsSuccess()
    {
        var note = new string('a', CustomerStatusNoteValidator.MaxLength);

        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ExceedsMaxLength_ReturnsFailure()
    {
        var note = new string('a', CustomerStatusNoteValidator.MaxLength + 1);

        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain(CustomerStatusNoteValidator.MaxLength.ToString());
    }

    [Fact]
    public void Validate_LengthMeasuredAfterTrim_TrailingWhitespaceDoesNotPushOverCap()
    {
        var note = new string('a', CustomerStatusNoteValidator.MaxLength) + "     ";

        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void MaxLength_DefaultsTo280()
    {
        CustomerStatusNoteValidator.MaxLength.Should().Be(280);
    }

    [Fact]
    public void Validate_CustomMaxLength_IsHonoured()
    {
        CustomerStatusNoteValidator.Validate(new string('a', 50), maxLength: 50).IsValid.Should().BeTrue();
        CustomerStatusNoteValidator.Validate(new string('a', 51), maxLength: 50).IsValid.Should().BeFalse();
    }
}
