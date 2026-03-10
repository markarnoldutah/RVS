using Newtonsoft.Json;

namespace RVS.Domain.Entities;

public class PayerConfig : EntityBase
{
    public override string Type { get; init; } = "payerConfig";

    [JsonProperty("payerId")]
    public required string PayerId { get; init; }

    /// <summary>
    /// Practice scope is REQUIRED for runtime payer configs.
    /// BF does not support tenant-default payer configs at runtime.
    /// </summary>
    [JsonProperty("practiceId")]
    public required string PracticeId { get; init; }

    [JsonProperty("sortOrder")]
    public int SortOrder { get; set; }

    [JsonProperty("displayName")]
    public string? DisplayName { get; set; }

    [JsonProperty("cobDefaultRole")]
    public string? CobDefaultRole { get; set; }
}
