using System.IO.Compression;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// Packs a generated project directory into the zip the customer downloads.
///
/// Two things this deliberately does that <c>ZipFile.CreateFromDirectory</c> does not:
///
/// 1. <b>It excludes build residue.</b> The Compile Guarantee now runs <c>npm ci</c> and
///    <c>npm run build</c> inside the very directory that gets packed, so the output tree
///    a customer would have received grew from ~3.4 MB to 525 MB / 25,488 files — a
///    Linux-x64 <c>node_modules/</c> (sharp, @next/swc-linux-x64-gnu), a <c>.next/</c>
///    cache, and a <c>dotnet/obj/project.assets.json</c> full of container paths. None of
///    that is the customer's codebase and none of it is portable to their machine.
///    Excluded directories are pruned during the walk, so 23k node_modules entries are
///    never even enumerated.
///
/// 2. <b>It streams to a temp file.</b> The previous implementation zipped into a
///    <see cref="MemoryStream"/>; on the post-build tree that is a ~141 MB allocation
///    (measured) on the LOH inside the engine container, per generation.
/// </summary>
internal static class ProjectArchiver
{
    /// <summary>
    /// Zips <paramref name="sourceDirectory"/> (minus build residue) to a new temp file and
    /// returns its path. The caller owns the file and must delete it.
    /// </summary>
    public static string CreateArchiveFile(string sourceDirectory, CancellationToken ct = default)
    {
        var zipPath = Path.Combine(
            Path.GetTempPath(), $"stackalchemist-archive-{Guid.NewGuid():N}.zip");

        try
        {
            using var file = new FileStream(
                zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            WriteArchive(sourceDirectory, file, ct);
        }
        catch
        {
            TryDelete(zipPath);
            throw;
        }

        return zipPath;
    }

    /// <summary>
    /// Writes the archive of <paramref name="sourceDirectory"/> into
    /// <paramref name="destination"/>. Entry names are relative and '/'-separated, so the
    /// zip unpacks identically on Windows, macOS, and Linux.
    /// </summary>
    public static void WriteArchive(string sourceDirectory, Stream destination, CancellationToken ct = default)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var relativePath in EnumerateArchiveEntries(sourceDirectory))
        {
            ct.ThrowIfCancellationRequested();
            archive.CreateEntryFromFile(
                Path.Combine(sourceDirectory, relativePath),
                relativePath,
                CompressionLevel.Optimal);
        }
    }

    /// <summary>
    /// The relative, '/'-separated paths that belong in the customer archive, sorted so an
    /// archive of the same tree is byte-order-stable regardless of filesystem enumeration
    /// order. Empty directories are dropped (a pruned <c>dotnet/bin/</c> is not worth an entry).
    /// </summary>
    public static IReadOnlyList<string> EnumerateArchiveEntries(string sourceDirectory)
    {
        var entries = new List<string>();
        Walk(sourceDirectory, relativeDirectory: string.Empty, entries);
        entries.Sort(StringComparer.Ordinal);
        return entries;
    }

    private static void Walk(string root, string relativeDirectory, List<string> entries)
    {
        var absoluteDirectory = relativeDirectory.Length == 0
            ? root
            : Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));

        foreach (var file in Directory.EnumerateFiles(absoluteDirectory))
            entries.Add(Join(relativeDirectory, Path.GetFileName(file)));

        foreach (var directory in Directory.EnumerateDirectories(absoluteDirectory))
        {
            var childRelative = Join(relativeDirectory, Path.GetFileName(directory));
            if (BuildResiduePaths.IsResidueDirectory(childRelative))
                continue;

            Walk(root, childRelative, entries);
        }
    }

    private static string Join(string relativeDirectory, string name) =>
        relativeDirectory.Length == 0 ? name : $"{relativeDirectory}/{name}";

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // A leftover temp zip is not worth failing a delivered generation over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
