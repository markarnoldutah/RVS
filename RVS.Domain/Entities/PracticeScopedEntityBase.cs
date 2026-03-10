using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Base for all Cosmos entities. Abstract to prevent EntityBase instantiation
/// </summary>
public abstract class PracticeScopedEntityBase : EntityBase
{
    /// <summary>
    /// All PHI entities must be associated with a practice for HIPAA compliance
    /// </summary>
    [JsonProperty("practiceId")]
    public required string PracticeId { get; init; }

}


