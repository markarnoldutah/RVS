using RVS.Domain.Packets;
using RVS.PacketDump.Html;

// Renders sample service packets to standalone HTML files so the print output can be
// checked on a real shop printer at Letter and A4 (issue #431, Spec B-3). Dev utility
// only — not part of any deployable artifact.
//
//   dotnet run --project RVS.PacketDump.HTML                 # writes ./packet-*.html
//   dotnet run --project RVS.PacketDump.HTML -- ~/Desktop    # writes into a directory
//   dotnet run --project RVS.PacketDump.HTML -- out.html --full
//
// Open the file in a browser and use its print dialog (Cmd/Ctrl-P) to choose paper size
// and print or save as PDF. The renderer deliberately leaves paper size to that dialog.

var (target, variant) = Args.Parse(args);

var samples = new List<(string Name, ServicePacket Packet)>();
if (variant is "full" or "both")
{
    samples.Add(("packet-full.html", SamplePackets.Full()));
}

if (variant is "minimal" or "both")
{
    samples.Add(("packet-minimal.html", SamplePackets.Minimal()));
}

foreach (var (name, packet) in samples)
{
    var path = target.IsDirectory ? Path.Combine(target.Value, name) : target.Value;
    var html = PacketHtmlRenderer.Render(packet);
    File.WriteAllText(path, html);
    Console.WriteLine($"wrote {Path.GetFullPath(path)}  ({html.Length:N0} chars)");
}
