namespace RVS.Domain.Shared;

public enum LookupOverrideMode
{
    /// <summary>
    /// MVP mode: only global sets exist; tenant-specific docs are ignored.
    /// </summary>
    GlobalOnly = 0,

    /// <summary>
    /// In the future: tenant items overlay onto the global set (merge by Code).
    /// </summary>
    Merge = 1,

    /// <summary>
    /// In the future: tenant set completely replaces the global set.
    /// </summary>
    ReplaceAll = 2
}





