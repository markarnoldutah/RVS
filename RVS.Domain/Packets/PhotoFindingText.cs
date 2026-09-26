using RVS.Domain.Entities;

namespace RVS.Domain.Packets;

/// <summary>
/// The wording of a photo finding (issue #772), shared by the packet's <c>From photos</c>
/// sub-block and the DMS paste block so a data plate reads the same in both.
/// </summary>
public static class PhotoFindingText
{
    /// <summary><c>Refrigerator — Dometic RM2652 · S/N 12345678</c>; parts not read are left out.</summary>
    public static string DataPlate(PhotoDataPlateEmbedded plate)
    {
        ArgumentNullException.ThrowIfNull(plate);

        var makeModel = string.Join(' ', new[] { plate.Manufacturer, plate.ModelNumber }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim()));
        var serial = string.IsNullOrWhiteSpace(plate.SerialNumber) ? null : $"S/N {plate.SerialNumber.Trim()}";

        var detail = string.Join(" · ", new[] { makeModel, serial }.Where(p => !string.IsNullOrEmpty(p)));
        return detail.Length == 0 ? plate.Component.Trim() : $"{plate.Component.Trim()} — {detail}";
    }

    /// <summary><c>Thermostat — code E1</c>, with <c>: meaning</c> when one is known.</summary>
    public static string FaultCode(PhotoFaultCodeEmbedded fault)
    {
        ArgumentNullException.ThrowIfNull(fault);

        var text = $"{fault.Component.Trim()} — code {fault.Code.Trim()}";
        return string.IsNullOrWhiteSpace(fault.Meaning) ? text : $"{text}: {fault.Meaning.Trim()}";
    }

    /// <summary>
    /// The paste-block lines for <paramref name="findings"/>: one <c>EQUIPMENT:</c> line per data
    /// plate, then one <c>FAULT CODE:</c> line per code. Observations are left out — a DMS
    /// complaint field wants the facts a part lookup needs, not a description of the photo.
    /// </summary>
    public static IReadOnlyList<string> PasteLines(PhotoFindingsEmbedded? findings)
    {
        if (findings is null)
        {
            return [];
        }

        return
        [
            .. findings.DataPlates.Select(p => $"EQUIPMENT: {DataPlate(p)}"),
            .. findings.FaultCodes.Select(f => $"FAULT CODE: {FaultCode(f)}"),
        ];
    }
}
