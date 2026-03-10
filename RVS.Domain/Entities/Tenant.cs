using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace RVS.Domain.Entities;

public class Tenant : EntityBase
{
    public override string Type { get; init; } = "tenant";

    // Override TenantId from EntityBase to always mirror Id for Tenant entities
    [JsonProperty("tenantId")]
    public override string TenantId => Id;

    [JsonProperty("billingEmail")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    public string? BillingEmail { get; set; }
    
    // default TBD
    [JsonProperty("status")]
    public string Status { get; set; } = default!;

    // default TBD
    [JsonProperty("plan")]
    public string Plan { get; set; } = default!;

    [JsonProperty("notes")]
    public string? Notes { get; set; }

}
