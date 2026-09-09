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
    [InlineData("This model water heater is recalled. We've ordered a replacement and will call when received.")]
    [InlineData("Advisor said: \"parts ship Tuesday\"; pickup after that.")]
    [InlineData("Path noted as C:\\jobs\\4821 in the DMS.")]
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
    [InlineData("<script>alert(1)</script>")]
    public void Validate_AngleBracket_ReturnsFailure(string note)
    {
        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("blocked character");
    }

    [Theory]
    [InlineData("null\0char")]
    [InlineData("bell\achar")]
    [InlineData("vertical\vtab")]
    public void Validate_ControlCharacter_ReturnsFailure(string note)
    {
        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("control character");
    }

    [Theory]
    [InlineData("semi; colon")]
    [InlineData("it's broken")]
    [InlineData("say \"hi\"")]
    [InlineData("back\\slash")]
    [InlineData("tab\tseparated")]
    public void Validate_OrdinaryPunctuationAndWhitespace_ReturnsSuccess(string note)
    {
        var result = CustomerStatusNoteValidator.Validate(note);

        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
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
