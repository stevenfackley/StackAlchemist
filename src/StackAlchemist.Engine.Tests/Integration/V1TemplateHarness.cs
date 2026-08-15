using System.IO.Abstractions;
using StackAlchemist.Engine.Models;
using StackAlchemist.Engine.Services;

namespace StackAlchemist.Engine.Tests.Integration;

/// <summary>
/// Renders the REAL on-disk <c>V1-DotNet-NextJs</c> template set to a directory using the
/// real <see cref="TemplateProvider"/> and <see cref="ReconstructionService"/> — the exact
/// two components the paid pipeline runs — with an empty LLM block set.
///
/// Why no LLM call: the one-shot Claude pass is <em>additive</em>. It fills injection zones
/// on top of this output. If this baseline does not compile, nothing downstream can, and
/// every paid generation is gambling a customer's money on a template nobody ever built.
/// Keeping the harness LLM-free also means it is deterministic, free, and safe to run on
/// every CI push.
///
/// Used by <see cref="V1TemplateCompileTests"/> (the CI gate) and by anyone who wants to
/// eyeball the baseline: set <c>SA_TEMPLATE_HARNESS_OUTPUT</c> to a directory and run that
/// test to have the rendered tree left on disk instead of a temp dir.
/// </summary>
internal static class V1TemplateHarness
{
    public const string TemplateSetName = "V1-DotNet-NextJs";

    /// <summary>Env var naming a directory to render into (and keep) instead of a temp dir.</summary>
    public const string OutputDirEnvVar = "SA_TEMPLATE_HARNESS_OUTPUT";

    /// <summary>
    /// The sample brief the harness renders. Deliberately a plausible multi-entity SaaS
    /// rather than a one-entity toy — a single entity would hide any per-entity template
    /// that only breaks when rendered more than once.
    /// </summary>
    public const string SampleBrief =
        "InvoiceHub — a small-business invoicing SaaS. Customers are billed via invoices; " +
        "each invoice has line items. Entities: Customer, Invoice, LineItem.";

    /// <summary>
    /// Resolves the templates root the same way the Engine host does, so the test fails
    /// loudly on a layout change instead of silently testing nothing.
    /// </summary>
    public static string ResolveTemplatesRoot() => TemplatesRootResolver.Resolve(
        AppContext.BaseDirectory,
        Directory.GetCurrentDirectory(),
        configuredRoot: null,
        Directory.Exists);

    /// <summary>
    /// Mirrors the shape <c>GenerationOrchestrator.BuildVariables</c> produces for the
    /// <see cref="SampleBrief"/> schema: PascalCase project + entity names, lowercase
    /// pluralised table names, and C#/SQL types from the same mapping tables.
    /// </summary>
    public static TemplateVariables SampleVariables() => new()
    {
        ProjectName = "InvoiceHub",
        ProjectNameKebab = "invoice-hub",
        ProjectNameLower = "invoicehub",
        DbConnectionString =
            "Host=localhost;Port=5432;Database=invoicehub;Username=postgres;Password=postgres",
        FrontendUrl = "http://localhost:3000",
        Entities =
        [
            new TemplateEntity
            {
                Name = "Customer",
                NameLower = "customer",
                TableName = "customers",
                Fields =
                [
                    Field("Id", "Guid", "UUID", isPrimaryKey: true),
                    Field("Name", "string", "TEXT"),
                    Field("Email", "string", "TEXT"),
                ],
            },
            new TemplateEntity
            {
                Name = "Invoice",
                NameLower = "invoice",
                TableName = "invoices",
                Fields =
                [
                    Field("Id", "Guid", "UUID", isPrimaryKey: true),
                    Field("CustomerId", "Guid", "UUID"),
                    Field("Number", "string", "TEXT"),
                    Field("Total", "decimal", "NUMERIC(10,2)"),
                    Field("IssuedAt", "DateTime", "TIMESTAMPTZ"),
                ],
            },
            new TemplateEntity
            {
                Name = "LineItem",
                NameLower = "lineitem",
                TableName = "lineitems",
                Fields =
                [
                    Field("Id", "Guid", "UUID", isPrimaryKey: true),
                    Field("InvoiceId", "Guid", "UUID"),
                    Field("Description", "string", "TEXT"),
                    Field("Quantity", "int", "INTEGER"),
                    Field("UnitPrice", "decimal", "NUMERIC(10,2)"),
                ],
            },
        ],
    };

    /// <summary>
    /// Loads → renders → reconstructs (with zero LLM blocks) → writes to
    /// <paramref name="outputDirectory"/>. Returns the written relative paths.
    /// </summary>
    public static IReadOnlyList<string> RenderTo(string outputDirectory)
    {
        var provider = new TemplateProvider(new FileSystem(), ResolveTemplatesRoot());
        var reconstruction = new ReconstructionService();

        var raw = provider.LoadTemplate(TemplateSetName);
        var rendered = provider.Render(raw, SampleVariables());

        // The production V1 path always goes through Reconstruct — an empty block map is
        // the honest "the LLM contributed nothing" case, and it is Reconstruct that
        // resolves and strips the [[LLM_INJECTION_*]] scaffolding.
        var files = reconstruction.Reconstruct(rendered, [], provider);

        Directory.CreateDirectory(outputDirectory);
        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(outputDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        return [.. files.Keys.OrderBy(p => p, StringComparer.Ordinal)];
    }

    private static TemplateField Field(string name, string type, string sqlType, bool isPrimaryKey = false) => new()
    {
        Name = name,
        NameLower = name.ToLowerInvariant(),
        Type = type,
        SqlType = sqlType,
        IsPrimaryKey = isPrimaryKey,
    };
}
