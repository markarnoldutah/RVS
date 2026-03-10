using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// Base for all Cosmos entities. Abstract to prevent EntityBase instantiation
/// </summary>
public abstract class EntityBase
{
    /// <summary>
    /// Abstract property must always be implemented by derived classes to return the type of the entity; settable only at initialization
    /// </summary>
    [JsonProperty("type")]
    public abstract string Type { get; init; }

    /// <summary>
    /// Gets the unique identifier for this instance, by default a new GUID is created and settable only at initialization
    /// </summary>
    [JsonProperty("id")]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the tenant identifier for this instance, by default a new GUID is created and settable only at initialization. 
    /// Can be set to "GLOBAL" for shared resources.
    /// Virtual to allow derived classes (e.g., Tenant) to override behavior.
    /// </summary>
    [JsonProperty("tenantId")]
    public virtual string TenantId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Virtual property allows derived classes to provide their own version of how to return a name
    /// </summary>
    [JsonProperty("name")]
    public virtual string Name { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether this entity is enabled for operational use by end user
    /// </summary>
    [JsonProperty("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets the date and time, in Coordinated Universal Time (UTC), when the object was created, settable only at initialization
    /// </summary>
    [JsonProperty("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }  = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time, in UTC, when the object was last updated.
    /// Null if never updated after creation.
    /// </summary>
    [JsonProperty("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets the user who created this entity. Null if creator is unknown (e.g., system-generated).
    /// </summary>
    [JsonProperty("createdByUserId")]
    public string? CreatedByUserId { get; init; }

    /// <summary>
    /// Gets or sets the user who last updated this entity. Null if never updated.
    /// </summary>
    [JsonProperty("updatedByUserId")]
    public string? UpdatedByUserId { get; set; }

    /// <summary>
    /// Updates the entity's modification tracking fields.
    /// Sets UpdatedAtUtc to current UTC time and UpdatedByUserId to the specified user.
    /// </summary>
    /// <param name="userId">The ID of the user making the update (null for system operations)</param>
    public void MarkAsUpdated(string? userId = null)
    {
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = userId;
    }
}


