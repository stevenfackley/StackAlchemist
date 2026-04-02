using System.IO.Abstractions;
using System.Text.RegularExpressions;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public sealed partial class TemplateProvider : ITemplateProvider
{
    private readonly IFileSystem _fs;
    private readonly string _templatesRoot;

    public TemplateProvider(IFileSystem fileSystem, string templatesRoot)
    {
        _fs = fileSystem;
        _templatesRoot = templatesRoot;
    }

    public Dictionary<string, string> LoadTemplate(string templateSetName)
    {
        var dir = _fs.Path.Combine(_templatesRoot, templateSetName);
        if (!_fs.Directory.Exists(dir))
        {
            var available = _fs.Directory.Exists(_templatesRoot)
                ? string.Join(", ", _fs.Directory.GetDirectories(_templatesRoot).Select(_fs.Path.GetFileName))
                : "none";
            throw new TemplateNotFoundException(
                $"Template set '{templateSetName}' not found. Available: {available}");
        }

        var result = new Dictionary<string, string>();
        foreach (var file in _fs.Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            var relativePath = _fs.Path.GetRelativePath(dir, file).Replace('\\', '/');
            result[relativePath] = _fs.File.ReadAllText(file);
        }

        return result;
    }

    public Dictionary<string, string> Render(Dictionary<string, string> templates, TemplateVariables variables)
    {
        var result = new Dictionary<string, string>();

        foreach (var (path, content) in templates)
        {
            var renderedPath = ApplyTemplateVariables(path, variables);
            var renderedContent = ApplyTemplateVariables(content, variables);
            result[renderedPath] = renderedContent;
        }

        return result;
    }

    public List<string> FindInjectionZones(string content)
    {
        return InjectionZoneRegex()
            .Matches(content)
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct()
            .ToList();
    }

    public string InjectIntoZone(string template, string zoneName, string content)
    {
        var pattern = $@"({{{{!-- LLM_INJECTION_START: {Regex.Escape(zoneName)} --}}}})(\r?\n)?(.*?)({{{{!-- LLM_INJECTION_END: {Regex.Escape(zoneName)} --}}}})";
        var replacement = $"${{1}}\n{content}\n${{4}}";
        var result = Regex.Replace(template, pattern, replacement, RegexOptions.Singleline);
        return result;
    }

    private static string ApplyTemplateVariables(string input, TemplateVariables vars)
    {
        // Handle {{#each Entities}}...{{/each}} blocks first
        var processed = EachBlockRegex().Replace(input, m =>
        {
            var varName = m.Groups[1].Value.Trim();
            var body = m.Groups[2].Value;
            if (varName == "Entities")
            {
                return string.Concat(vars.Entities.Select(entity =>
                    body
                        .Replace("{{Name}}", entity.Name)
                        .Replace("{{NameLower}}", entity.NameLower)
                        .Replace("{{TableName}}", entity.TableName)));
            }
            return string.Empty;
        });

        // Scalar substitutions
        return processed
            .Replace("{{ProjectName}}", vars.ProjectName)
            .Replace("{{ProjectNameKebab}}", vars.ProjectNameKebab)
            .Replace("{{ProjectNameLower}}", vars.ProjectNameLower)
            .Replace("{{DbConnectionString}}", vars.DbConnectionString)
            .Replace("{{FrontendUrl}}", vars.FrontendUrl);
    }

    [GeneratedRegex(@"\{\{!-- LLM_INJECTION_START:\s*(.+?)\s*--\}\}")]
    private static partial Regex InjectionZoneRegex();

    [GeneratedRegex(@"\{\{#each\s+(\w+)\}\}(.*?)\{\{/each\}\}", RegexOptions.Singleline)]
    private static partial Regex EachBlockRegex();
}
