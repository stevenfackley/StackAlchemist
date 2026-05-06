using System.IO.Abstractions;
using System.Text.RegularExpressions;
using HandlebarsDotNet;
using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public sealed partial class TemplateProvider : ITemplateProvider
{
    private const string EntityNameToken = "{{EntityName}}";

    private readonly IFileSystem _fs;
    private readonly string _templatesRoot;
    private readonly IHandlebars _handlebars;

    public TemplateProvider(IFileSystem fileSystem, string templatesRoot)
    {
        _fs = fileSystem;
        _templatesRoot = templatesRoot;
        _handlebars = Handlebars.Create();
        _handlebars.Configuration.NoEscape = true;
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
            if (path.Contains(EntityNameToken, StringComparison.Ordinal))
            {
                // Per-entity template: render once per schema entity.
                foreach (var entity in variables.Entities)
                {
                    var ctx = BuildEntityContext(variables, entity);
                    var renderedPath = RenderString(path, ctx);
                    var renderedContent = RenderString(content, ctx);
                    result[renderedPath] = renderedContent;
                }
            }
            else
            {
                // Schema-wide template: render once with full variables.
                var renderedPath = RenderString(path, variables);
                var renderedContent = RenderString(content, variables);
                result[renderedPath] = renderedContent;
            }
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
        var pattern = $@"(\[\[LLM_INJECTION_START:\s*{Regex.Escape(zoneName)}\s*\]\])(\r?\n)?(.*?)(\[\[LLM_INJECTION_END:\s*{Regex.Escape(zoneName)}\s*\]\])";
        var replacement = $"${{1}}\n{content}\n${{4}}";
        return Regex.Replace(template, pattern, replacement, RegexOptions.Singleline);
    }

    public string StripInjectionMarkers(string content)
    {
        return InjectionMarkerLineRegex().Replace(content, string.Empty);
    }

    private string RenderString(string input, object ctx)
    {
        var template = _handlebars.Compile(input);
        return template(ctx);
    }

    private static object BuildEntityContext(TemplateVariables variables, TemplateEntity entity) => new
    {
        variables.ProjectName,
        variables.ProjectNameKebab,
        variables.ProjectNameLower,
        variables.DbConnectionString,
        variables.FrontendUrl,
        variables.Entities,
        EntityName = entity.Name,
        EntityNameLower = entity.NameLower,
        TableName = entity.TableName,
        Fields = entity.Fields,
    };

    [GeneratedRegex(@"\[\[LLM_INJECTION_START:\s*(.+?)\s*\]\]")]
    private static partial Regex InjectionZoneRegex();

    [GeneratedRegex(@"^[ \t]*\[\[LLM_INJECTION_(START|END):\s*[^\]]+\]\][ \t]*\r?\n?", RegexOptions.Multiline)]
    private static partial Regex InjectionMarkerLineRegex();
}
