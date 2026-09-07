namespace RVS.PacketDump.Html;

/// <summary>Where output should go, and whether that path is a directory or a single file.</summary>
internal readonly record struct Target(string Value, bool IsDirectory);

/// <summary>Minimal command-line parsing for the packet dump utility.</summary>
internal static class Args
{
    /// <summary>
    /// Parses <c>[path] [--variant full|minimal|both] [--full] [--minimal]</c>.
    /// A path that exists as a directory, ends in a separator, or has no <c>.html</c>
    /// extension is treated as a directory; anything else is a single output file.
    /// When a single file is named, the variant defaults to <c>full</c>; otherwise
    /// both variants are written.
    /// </summary>
    public static (Target Target, string Variant) Parse(string[] args)
    {
        string? path = null;
        string? variant = null;

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
                default:
                    if (!arg.StartsWith('-'))
                    {
                        path = arg;
                    }

                    break;
            }
        }

        var target = ResolveTarget(path);
        variant ??= target.IsDirectory ? "both" : "full";

        if (variant is not ("full" or "minimal" or "both"))
        {
            variant = "both";
        }

        return (target, variant);
    }

    private static Target ResolveTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new Target(Directory.GetCurrentDirectory(), IsDirectory: true);
        }

        var looksLikeDirectory =
            Directory.Exists(path)
            || path.EndsWith(Path.DirectorySeparatorChar)
            || path.EndsWith(Path.AltDirectorySeparatorChar)
            || !path.EndsWith(".html", StringComparison.OrdinalIgnoreCase);

        if (looksLikeDirectory)
        {
            Directory.CreateDirectory(path);
            return new Target(path, IsDirectory: true);
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return new Target(path, IsDirectory: false);
    }
}
