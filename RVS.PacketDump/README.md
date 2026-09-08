# RVS.PacketDump

Dev utility. Renders sample [`ServicePacket`](../RVS.Domain/Packets/ServicePacket.cs)
instances to standalone files so the print output can be checked on a real shop printer
at Letter and A4 (`Spec B-3`):

- **HTML** via [`PacketHtmlRenderer`](../RVS.Domain/Packets/PacketHtmlRenderer.cs) (issue #431)
- **PDF** via [`PacketPdfRenderer`](../RVS.API/Packets/PacketPdfRenderer.cs) (issue #432)

Dumps both formats. Not referenced by any app and not part of a deployable artifact;
it references `RVS.API` only to reach the PDF renderer.

## Use

```bash
# Both formats, both variants, into the current directory
dotnet run --project RVS.PacketDump

# Into a directory
dotnet run --project RVS.PacketDump -- ~/Desktop

# One PDF of the full packet
dotnet run --project RVS.PacketDump -- --pdf --full

# A named file — the extension fixes the format
dotnet run --project RVS.PacketDump -- ~/Desktop/packet.pdf

# Print the full switch list and exit
dotnet run --project RVS.PacketDump -- --help
```

### Switches

| Switch | Values | Default |
|---|---|---|
| `--format` (or `--html` / `--pdf`) | `html`, `pdf`, `both` | `both` |
| `--variant` (or `--full` / `--minimal`) | `full`, `minimal`, `both` | `both` |
| `-h`, `--help`, `-?`, `/?` | prints usage and exits | — |
| _positional_ | output path — a directory, or a file ending `.html` / `.pdf` | current directory |

A named output file's extension overrides `--format` and pins `--variant` to `full`
(one file out). A directory writes `packet-<variant>.<format>` for every selected
combination.

Then: open an HTML file in a browser and print with its own dialog (Cmd/Ctrl-P) to
choose paper size, or "Save as PDF". The HTML renderer sets margins only and leaves paper
size to that dialog; the PDF has a fixed page box sized to the A4 ∩ Letter intersection
(210 × 279 mm, 14 mm margins) so it prints inside the margins of either sheet.

## Samples

- **full** — every section populated; the packet at its longest.
- **minimal** — the maximally degraded packet the composer can still emit: no VIN,
  category, diagnostics, AI summary, photos, paste block, or status link.

PDF photos render as labelled placeholder cells here — resolving the time-limited SAS
URLs to image bytes is the orchestrator's job (issue #433).
