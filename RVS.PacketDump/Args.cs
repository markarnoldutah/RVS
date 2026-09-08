namespace RVS.PacketDump;

/// <summary>Where output should go, and whether that path is a directory or a single file.</summary>
internal readonly record struct Target(string Value, bool IsDirectory);

/// <summary>Parsed command line: output target, which sample packet(s), which format(s).</summary>
internal readonly record struct Options(Target Target, string Variant, string Format);

/// <summary>Minimal command-line parsing for the packet dump utility.</summary>
internal static class Args
{
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
