using Newtonsoft.Json;

namespace RVS.Domain.Entities;

public class Location : EntityBase
{
    public override string Type { get; init; } = "location";

    // MIRROR of Id
    [JsonProperty("locationId")]
    public string LocationId => Id;

    [JsonProperty("address1")]
    public string? Address1 { get; set; }

    [JsonProperty("address2")]
    public string? Address2 { get; set; }

    [JsonProperty("city")]
    public string? City { get; set; }

    [JsonProperty("state")]
    public string? State { get; set; }

    [JsonProperty("postalCode")]
    public string? PostalCode { get; set; }

    [JsonProperty("phone")]
    public string? Phone { get; set; }

}