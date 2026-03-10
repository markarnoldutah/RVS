using Newtonsoft.Json;


namespace RVS.Domain.Entities;

public class Payer : EntityBase
{
    public override string Type { get; init; } = "payer";

    [JsonProperty("payerId")]
    public string PayerId => Id;

    /// <summary>
    /// Plan types this payer can represent (e.g., "Vision", "Medical").
    /// Coverage enrollment still records the selected plan type per enrollment.
    /// </summary>
    [JsonProperty("supportedPlanTypes")]
    public List<string> SupportedPlanTypes { get; set; } = new();

    [JsonProperty("availityPayerCode")]
    public string? AvailityPayerCode { get; set; }

    [JsonProperty("x12PayerId")]
    public string? X12PayerId { get; set; }

    [JsonProperty("isMedicare")]
    public bool IsMedicare { get; set; }

    [JsonProperty("isMedicaid")]
    public bool IsMedicaid { get; set; }
}
