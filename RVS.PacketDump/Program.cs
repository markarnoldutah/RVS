using RVS.API.Packets;
using RVS.Domain.Packets;
using RVS.PacketDump;

// Renders sample service packets to standalone files so the print output can be checked
// on a real shop printer at Letter and A4 (issues #431 HTML / #432 PDF, Spec B-3).
// Dev utility only — not part of any deployable artifact.
//
//   dotnet run --project RVS.PacketDump                      # ./packet-*.html + .pdf
//   dotnet run --project RVS.PacketDump -- ~/Desktop         # into a directory
//   dotnet run --project RVS.PacketDump -- --pdf --full      # one PDF, full packet
//   dotnet run --project RVS.PacketDump -- ~/Desktop/p.html  # extension fixes format
//
// Switches:
//   --format html|pdf|both   (default both; also --html / --pdf)
//   --variant full|minimal|multi|both|all   (default both; also --full / --minimal / --multi)
//   -h | --help | -? | /?    print the full switch list and exit
//
// Open an HTML file in a browser and use its print dialog (Cmd/Ctrl-P) to choose paper
// size; the renderer leaves paper size to that dialog. The PDF has a fixed page box
// sized to the A4 ∩ Letter intersection, so it prints inside the margins of either.

var options = Args.Parse(args);

if (options.ShowHelp)
{
    Console.WriteLine(Args.HelpText);
    return;
}

var variants = new List<(string Name, IReadOnlyList<ServicePacket> Packets)>();
if (options.Variant is "full" or "both" or "all")
{
    variants.Add(("full", [SamplePackets.Full()]));
}

if (options.Variant is "minimal" or "both" or "all")
{
    variants.Add(("minimal", [SamplePackets.Minimal()]));
}

// Several issues on one visit, one packet (Spec A-17, issue #806).
if (options.Variant is "multi" or "all")
{
    variants.Add(("multi", SamplePackets.Multi()));
}

var formats = new List<string>();
if (options.Format is "html" or "both")
{
    formats.Add("html");
}

if (options.Format is "pdf" or "both")
{
    formats.Add("pdf");
}

foreach (var (name, packets) in variants)
{
    foreach (var format in formats)
    {
        var path = options.Target.IsDirectory
            ? Path.Combine(options.Target.Value, $"packet-{name}.{format}")
            : options.Target.Value;

        if (format == "html")
        {
            var html = PacketHtmlRenderer.RenderCombined(packets);
            File.WriteAllText(path, html);
            Console.WriteLine($"wrote {Path.GetFullPath(path)}  ({html.Length:N0} chars)");
        }
        else
        {
            var pdf = PacketPdfRenderer.RenderCombined(packets);
            File.WriteAllBytes(path, pdf);
            Console.WriteLine($"wrote {Path.GetFullPath(path)}  ({pdf.Length:N0} bytes)");
        }
    }
}
