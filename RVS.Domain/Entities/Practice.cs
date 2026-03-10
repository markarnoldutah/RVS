using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace RVS.Domain.Entities;

public class Practice : EntityBase
{
    public override string Type { get; init; } = "practice";

    [JsonProperty("practiceId")]
    public string PracticeId => Id;

    [JsonProperty("externalRef")]
    public string? ExternalRef { get; set; }

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    [JsonProperty("email")]
    [EmailAddress(ErrorMessage = "Invalid email address format")]
    public string? Email { get; set; }

    [JsonProperty("locations")]
    public List<Location> Locations { get; set; } = new();
}
