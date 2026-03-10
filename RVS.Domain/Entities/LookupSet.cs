using RVS.Domain.Entities;
using RVS.Domain.Shared;
using Newtonsoft.Json;

namespace RVS.Domain.Entities;

/// <summary>
/// A named set of lookup items (e.g., "encounter-types").
/// Stored in the Lookups container, partitioned by tenantId.
/// For MVP, only tenantId = "GLOBAL" is used, but the model supports tenant overrides later.
/// </summary>
public class LookupSet : EntityBase
{
    public override string Type { get; init; } = "lookupset";

    [JsonProperty("lookupSetId")]
    public string LookupSetId => Id;

    [JsonProperty("category")]
    public required string Category { get; init; }
    
    /// <summary>
    /// Optional description/help text.
    /// </summary>
    [JsonProperty("description")]
    public string? Description { get; set; }
    
    /// <summary>
    /// Future: How tenant overrides interact with global.
    /// For MVP, only "Global" is actually used.
    /// </summary>
    [JsonProperty("overrideMode")]
    public LookupOverrideMode OverrideMode { get; set; } = LookupOverrideMode.GlobalOnly;

    /// <summary>
    /// The lookup items for this set.
    /// In MVP, this is the full global list.
    /// In the future, tenant sets can add/override/hide items.
    /// </summary>
    [JsonProperty("items")]
    public List<LookupItem> Items { get; set; } = new();

}

// ---------------------------------------------------------------------------
// This is an item within a LookupSet's Items collection.
// ---------------------------------------------------------------------------
public class LookupItem
{
    /// <summary>
    /// Stable code that callers store/use, e.g. "routine-exam".
    /// </summary>
    [JsonProperty("code")]
    public required string Code { get; init; }

    /// <summary>
    /// Display name shown in UI dropdowns, e.g. "Routine Eye Exam".
    /// </summary>
    [JsonProperty("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Optional description/help text.
    /// </summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>
    /// For ordering in dropdowns.
    /// </summary>
    [JsonProperty("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this item is selectable in UI dropdowns.
    /// </summary>
    [JsonProperty("isSelectable")]
    public bool IsSelectable { get; set; } = true;

    /// <summary>
    /// Future: when tenant overrides global, this flag can "hide" the item.
    /// For MVP (Global-only), this is always false.
    /// </summary>
    [JsonProperty("isDeleted")]
    public bool IsDeleted { get; set; } = false;
}





