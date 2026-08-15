using System.IO.Compression;
using FluentAssertions;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Services;

/// <summary>
/// The customer archive is the product. These tests pin what may and may not be inside it.
///
/// The Compile Guarantee runs <c>npm ci</c> + <c>npm run build</c> inside the very directory
/// that gets zipped, so the naive <c>ZipFile.CreateFromDirectory</c> the uploader used to call
/// would have shipped a 141 MB archive containing a Linux-x64 <c>node_modules/</c>, a
/// <c>.next/</c> cache, and a <c>dotnet/obj/project.assets.json</c> full of container paths.
/// </summary>
public sealed class ProjectArchiverTests : IDisposable
{
    private readonly string _root = Directory
        .CreateTempSubdirectory("sa-archiver-" + Guid.NewGuid().ToString("N")[..8])
        .FullName;

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* best effort cleanup */ }
    }

    private void Write(string relativePath, string content = "x")
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void EnumerateArchiveEntries_ExcludesEveryFlavourOfBuildResidue()
    {
        Write("dotnet/Program.cs");
        Write("dotnet/InvoiceHub.csproj");
        Write("dotnet/obj/project.assets.json");
        Write("dotnet/obj/Debug/net10.0/InvoiceHub.AssemblyInfo.cs");
        Write("dotnet/bin/Debug/net10.0/InvoiceHub.dll");
        Write("nextjs/package.json");
        Write("nextjs/node_modules/next/package.json");
        Write("nextjs/node_modules/@next/swc-linux-x64-gnu/next-swc.node");
        Write("nextjs/.next/standalone/server.js");
        Write(".git/HEAD");
        Write("README.md");

        var entries = ProjectArchiver.EnumerateArchiveEntries(_root);

        entries.Should().BeEquivalentTo(
            "README.md",
            "dotnet/InvoiceHub.csproj",
            "dotnet/Program.cs",
            "nextjs/package.json");
    }

    [Fact]
    public void EnumerateArchiveEntries_KeepsSourceThatOnlyLooksLikeResidue()
    {
        // `bin` is a legitimate source directory for a CDK app entrypoint; only
        // bin/<Configuration>/ is .NET output. `obj.cs` is a file, not a directory.
        Write("infra/cdk/bin/app.ts");
        Write("dotnet/Models/obj.cs");
        Write("dotnet/objects/Thing.cs");
        Write("dotnet/bin/Debug/net10.0/InvoiceHub.dll");

        var entries = ProjectArchiver.EnumerateArchiveEntries(_root);

        entries.Should().BeEquivalentTo(
            "dotnet/Models/obj.cs",
            "dotnet/objects/Thing.cs",
            "infra/cdk/bin/app.ts");
    }

    [Fact]
    public void EnumerateArchiveEntries_UsesForwardSlashesAndStableOrder()
    {
        Write("b/second.cs");
        Write("a/first.cs");
        Write("top.md");

        var entries = ProjectArchiver.EnumerateArchiveEntries(_root);

        entries.Should().Equal("a/first.cs", "b/second.cs", "top.md");
        entries.Should().OnlyContain(e => !e.Contains('\\', StringComparison.Ordinal),
            "zip entry names are '/'-separated so the archive unpacks the same on every OS");
    }

    [Fact]
    public void CreateArchiveFile_WritesToDiskNotMemoryAndOmitsResidue()
    {
        Write("dotnet/Program.cs", "Console.WriteLine(\"hi\");");
        Write("nextjs/package.json", "{}");
        Write("nextjs/node_modules/next/package.json", new string('n', 200_000));
        Write("nextjs/.next/cache/blob", new string('c', 200_000));

        var zipPath = ProjectArchiver.CreateArchiveFile(_root);

        try
        {
            File.Exists(zipPath).Should().BeTrue("the archive is streamed to a temp file, never buffered in a MemoryStream");

            using var archive = ZipFile.OpenRead(zipPath);
            archive.Entries.Select(e => e.FullName).Should().BeEquivalentTo(
                "dotnet/Program.cs",
                "nextjs/package.json");

            new FileInfo(zipPath).Length.Should().BeLessThan(10_000,
                "excluding node_modules/.next is what keeps the customer's download small");
        }
        finally
        {
            ProjectArchiver.TryDelete(zipPath);
        }

        File.Exists(zipPath).Should().BeFalse("TryDelete is what stops temp zips accumulating in the engine container");
    }

    [Fact]
    public void CreateArchiveFile_RoundTripsFileContentExactly()
    {
        const string content = "public record Invoice(Guid Id, decimal Total);\n";
        Write("dotnet/Models/Invoice.cs", content);

        var zipPath = ProjectArchiver.CreateArchiveFile(_root);

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry("dotnet/Models/Invoice.cs");
            entry.Should().NotBeNull();

            using var reader = new StreamReader(entry!.Open());
            reader.ReadToEnd().Should().Be(content);
        }
        finally
        {
            ProjectArchiver.TryDelete(zipPath);
        }
    }
}
