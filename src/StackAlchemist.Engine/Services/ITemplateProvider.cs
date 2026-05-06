using StackAlchemist.Engine.Models;

namespace StackAlchemist.Engine.Services;

public interface ITemplateProvider
{
    /// <summary>
    /// Load all template files for the given template set (e.g. "V1-DotNet-NextJs").
    /// Returns dictionary of relative path → raw template content.
    /// </summary>
    Dictionary<string, string> LoadTemplate(string templateSetName);

    /// <summary>
    /// Render Handlebars variables in both file paths and file contents.
    /// Returns dictionary of rendered path → rendered content.
    /// </summary>
    Dictionary<string, string> Render(Dictionary<string, string> templates, TemplateVariables variables);

    /// <summary>
    /// Find all LLM injection zone names in the given content.
    /// </summary>
    List<string> FindInjectionZones(string content);

    /// <summary>
    /// Inject content between LLM_INJECTION_START and LLM_INJECTION_END markers for the given zone.
    /// </summary>
    string InjectIntoZone(string template, string zoneName, string content);

    /// <summary>
    /// Remove all [[LLM_INJECTION_START/END: zone]] marker lines from the content.
    /// Call after all per-zone injections are complete so generated output compiles.
    /// </summary>
    string StripInjectionMarkers(string content);
}
