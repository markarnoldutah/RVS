using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record TenantConfigCreateRequestDto
    {
        [Required(ErrorMessage = "Practice settings are required.")]
        public PracticeSettingsUpdateDto Practice { get; init; } = new();

        [Required(ErrorMessage = "Encounter settings are required.")]
        public EncounterSettingsUpdateDto Encounters { get; init; } = new();

        [Required(ErrorMessage = "Eligibility settings are required.")]
        public EligibilitySettingsUpdateDto Eligibility { get; init; } = new();

        [Required(ErrorMessage = "COB settings are required.")]
        public CobSettingsUpdateDto Cob { get; init; } = new();

        [Required(ErrorMessage = "UI settings are required.")]
        public UiSettingsUpdateDto Ui { get; init; } = new();
    }
}
