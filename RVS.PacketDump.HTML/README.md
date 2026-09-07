# RVS.PacketDump.HTML

Dev utility. Renders sample [`ServicePacket`](../RVS.Domain/Packets/ServicePacket.cs)
instances through [`PacketHtmlRenderer`](../RVS.Domain/Packets/PacketHtmlRenderer.cs) to
standalone `.html` files, so the print output can be checked on a real shop printer at
Letter and A4 (`Spec B-3`, issue #431).

Not referenced by any app and not part of a deployable artifact.

## Use

```bash
# Write packet-full.html + packet-minimal.html into the current directory
dotnet run --project RVS.PacketDump.HTML

# Write both into a directory
dotnet run --project RVS.PacketDump.HTML -- ~/Desktop

# Write one named file
dotnet run --project RVS.PacketDump.HTML -- ~/Desktop/packet.html --full
```

Options: a path (file or directory), and `--full` / `--minimal` / `--variant both`
(default: both when a directory is given, `full` when a single file is named).

Then open the file in a browser and print with the browser's own dialog (Cmd/Ctrl-P) to
choose paper size, or "Save as PDF". The renderer sets margins only and leaves paper size
to that dialog, so the same file prints on Letter and A4.

## Samples

- **`packet-full.html`** — every section populated; the packet at its longest.
- **`packet-minimal.html`** — the maximally degraded packet the composer can still emit:
  no VIN, category, diagnostics, AI summary, photos, paste block, or status link.
