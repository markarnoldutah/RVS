namespace RVS.PacketDump;

/// <summary>Where output should go, and whether that path is a directory or a single file.</summary>
internal readonly record struct Target(string Value, bool IsDirectory);

/// <summary>Parsed command line: output target, which sample packet(s), which format(s).</summary>
/// <remarks>When <see cref="ShowHelp"/> is true the other fields are unset and nothing should be rendered.</remarks>
internal readonly record struct Options(Target Target, string Variant, string Format, bool ShowHelp = false);

/// <summary>Minimal command-line parsing for the packet dump utility.</summary>
internal static class Args
{
    /// <summary>Usage text printed for <c>--help</c> / <c>-h</c> / <c>-?</c> / <c>/?</c>.</summary>
    public const string HelpText = """
        RVS.PacketDump — renders sample ServicePackets to standalone .html / .pdf files
        for print-testing at Letter and A4 (dev utility, not deployable).

        Usage:
          dotnet run --project RVS.PacketDump -- [path] [options]

        Arguments:
          path                     Output location. A directory (or a path that does not
                                   end in .html/.pdf) receives packet-<variant>.<format>
                                   for every selected combination. A path ending in
                                   .html or .pdf is a single output file: its extension
                                   fixes the format and pins the variant to 'full'.
                                   Defaults to the current directory.

        Options:
          --format <html|pdf|both> Which renderer(s) to run. Default: both.
          --html                   Shorthand for --format html.
          --pdf                    Shorthand for --format pdf.
          --variant <full|minimal|both>
                                   Which sample packet(s) to render. Default: both.
          --full                   Shorthand for --variant full.
          --minimal                Shorthand for --variant minimal.
          -h, --help, -?, /?       Print this help and exit.

        Examples:
          dotnet run --project RVS.PacketDump
          dotnet run --project RVS.PacketDump -- ~/Desktop
          dotnet run --project RVS.PacketDump -- --pdf --full
          dotnet run --project RVS.PacketDump -- ~/Desktop/packet.pdf
        """;

    /// <summary>
    /// Parses <c>[path] [--variant full|minimal|both] [--full] [--minimal]
    /// [--format html|pdf|both] [--html] [--pdf]</c>.
    ///
    /// A path that exists as a directory, ends in a separator, or has neither a
    /// <c>.html</c> nor a <c>.pdf</c> extension is treated as a directory; anything else
    /// is a single output file. When a single file is named its extension fixes the
    /// format and the variant defaults to <c>full</c>; otherwise both formats and both
    /// variants are written.
    /// </summary>
    public static Options Parse(string[] args)
    {
        string? path = null;
        string? variant = null;
        string? format = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help" or "-h" or "-?" or "/?":
                    return new Options(default, string.Empty, string.Empty, ShowHelp: true);
                case "--full":
                    variant = "full";
                    break;
                case "--minimal":
                    variant = "minimal";
                    break;
                case "--variant" when i + 1 < args.Length:
                    variant = args[++i].ToLowerInvariant();
                    break;
                case "--html":
                    format = "html";
                    break;
                case "--pdf":
                    format = "pdf";
                    break;
                case "--format" when i + 1 < args.Length:
                    format = args[++i].ToLowerInvariant();
                    break;
                default:
                    if (!arg.StartsWith('-'))
                    {
                        path = arg;
                    }

                    break;
            }
        }

        var target = ResolveTarget(path, out var fileExtension);

        // A named file's extension wins over --format and pins the variant to one.
        if (fileExtension is not null)
        {
            format = fileExtension;
            variant ??= "full";
        }

        variant = Normalise(variant, "both", "full", "minimal");
        format = Normalise(format, "both", "html", "pdf");

        return new Options(target, variant, format);
    }

    private static string Normalise(string? value, params string[] allowed) =>
        value is not null && Array.Exists(allowed, a => a == value) ? value : allowed[0];

    private static Target ResolveTarget(string? path, out string? fileExtension)
    {
        fileExtension = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            return new Target(Directory.GetCurrentDirectory(), IsDirectory: true);
        }

        var isHtml = path.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        var isPdf = path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

        var looksLikeDirectory =
            Directory.Exists(path)
            || path.EndsWith(Path.DirectorySeparatorChar)
            || path.EndsWith(Path.AltDirectorySeparatorChar)
            || !(isHtml || isPdf);

        if (looksLikeDirectory)
        {
            Directory.CreateDirectory(path);
            return new Target(path, IsDirectory: true);
        }

        fileExtension = isPdf ? "pdf" : "html";

        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return new Target(path, IsDirectory: false);
    }
}
