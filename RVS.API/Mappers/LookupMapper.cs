using RVS.Domain.DTOs;
using RVS.Domain.Entities;

namespace RVS.API.Mappers;

/// <summary>
/// Maps between Lookup entities and DTOs at the API boundary
/// </summary>
public static class LookupMapper
{
    public static LookupSetDto ToDto(this LookupSet entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var items = entity.Items
            .Where(i => !i.IsDeleted && i.IsSelectable)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .Select(i => new LookupItemDto(
                i.Code,
                i.Name,
                i.Description,
                i.SortOrder,
                i.IsSelectable))
            .ToList();

        return new LookupSetDto(
            Category: entity.Category,
            Name: entity.Name,
            Items: items
        );
    }

}


