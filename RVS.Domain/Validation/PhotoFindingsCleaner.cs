using RVS.Domain.Entities;

namespace RVS.Domain.Validation;

/// <summary>
/// Cleans the photo findings a model returns before they are stored on the request (issue #772).
/// Every string is trimmed; a blank or overlong field counts as unread (the prompt tells the model
/// to leave out what it cannot read, and this holds it to that); an entry with nothing usable, or
/// citing an attachment that is not on the request, is dropped; each list is capped at
/// <see cref="MaxPerList"/>, in the order given.
/// </summary>
public static class PhotoFindingsCleaner
{
    /// <summary>Most entries kept in each of the three lists.</summary>
    public const int MaxPerList = 5;

    /// <summary>Longest component, manufacturer, model, serial, code or meaning kept.</summary>
    public const int MaxFieldLength = 60;

    /// <summary>Longest observation kept.</summary>
    public const int MaxObservationLength = 240;

    /// <summary>
    /// Returns the cleaned findings, or <c>null</c> when <paramref name="raw"/> is <c>null</c> or
    /// nothing in it survives.
    /// </summary>
    /// <param name="raw">The findings as parsed from the model.</param>
    /// <param name="knownAttachmentIds">The attachment ids actually on the request.</param>
    public static PhotoFindingsEmbedded? Clean(PhotoFindingsEmbedded? raw, IEnumerable<string> knownAttachmentIds)
    {
        ArgumentNullException.ThrowIfNull(knownAttachmentIds);

        if (raw is null)
        {
            return null;
        }

        var known = new HashSet<string>(knownAttachmentIds, StringComparer.Ordinal);
        string? Cite(string? attachmentId) => Field(attachmentId, int.MaxValue) is { } id && known.Contains(id) ? id : null;

        var dataPlates = new List<PhotoDataPlateEmbedded>();
        foreach (var plate in raw.DataPlates ?? [])
        {
            if (plate is null || Field(plate.Component) is not { } component || Cite(plate.AttachmentId) is not { } id)
            {
                continue;
            }

            var cleaned = new PhotoDataPlateEmbedded
            {
                Component = component,
                Manufacturer = Field(plate.Manufacturer),
                ModelNumber = Field(plate.ModelNumber),
                SerialNumber = Field(plate.SerialNumber),
                AttachmentId = id,
            };

            if (cleaned.Manufacturer is not null || cleaned.ModelNumber is not null || cleaned.SerialNumber is not null)
            {
                dataPlates.Add(cleaned);
            }
        }

        var faultCodes = new List<PhotoFaultCodeEmbedded>();
        foreach (var fault in raw.FaultCodes ?? [])
        {
            if (fault is null || Field(fault.Component) is not { } component || Field(fault.Code) is not { } code
                || Cite(fault.AttachmentId) is not { } id)
            {
                continue;
            }

            faultCodes.Add(new PhotoFaultCodeEmbedded
            {
                Component = component,
                Code = code,
                Meaning = Field(fault.Meaning),
                AttachmentId = id,
            });
        }

        var observations = new List<PhotoObservationEmbedded>();
        foreach (var observation in raw.Observations ?? [])
        {
            if (observation is null || Field(observation.Text, MaxObservationLength) is not { } text
                || Cite(observation.AttachmentId) is not { } id)
            {
                continue;
            }

            observations.Add(new PhotoObservationEmbedded { Text = text, AttachmentId = id });
        }

        if (dataPlates.Count == 0 && faultCodes.Count == 0 && observations.Count == 0)
        {
            return null;
        }

        return new PhotoFindingsEmbedded
        {
            DataPlates = [.. dataPlates.Take(MaxPerList)],
            FaultCodes = [.. faultCodes.Take(MaxPerList)],
            Observations = [.. observations.Take(MaxPerList)],
        };
    }

    private static string? Field(string? value, int maxLength = MaxFieldLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength ? null : trimmed;
    }
}
