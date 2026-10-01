using System.Collections;

namespace StackAlchemist.Engine.Services;

/// <summary>
/// The environment a Compile Guarantee child process (dotnet, npm, npx, python, pip, flake8,
/// pytest) is started with: an <em>allowlist</em> of what a toolchain needs, never the Engine's
/// own environment.
///
/// Those children run LLM-generated code — a generated <c>.csproj</c> can declare MSBuild
/// <c>Exec</c> targets, a generated FastAPI app reads <c>DATABASE_URL</c> through
/// pydantic-settings and its tests import it, <c>npm install</c> runs package scripts. The
/// Engine's environment holds <c>DATABASE_URL</c> (the platform database), the Stripe and
/// Anthropic keys, the R2 credentials, <c>ENGINE_SERVICE_KEY</c> and <c>BYOK_ENCRYPTION_KEY</c>.
/// Inheriting it handed all of that to customer-shaped code (#451). An allowlist, not a deny
/// list: a secret added to the Engine later is withheld by default.
///
/// Residual, not addressed here: a child runs as the Engine's user, so it can still read the
/// Engine's environment from <c>/proc/&lt;pid&gt;/environ</c>, and it has network access. This
/// removes inheritance — accidental use and the easy path — not a determined escape; that needs
/// the build in its own sandbox (separate user/container, no network). Tracked separately.
/// </summary>
internal static class BuildProcessEnvironment
{
    /// <summary>Exact names (case-insensitive) a toolchain needs.</summary>
    private static readonly HashSet<string> AllowedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // POSIX basics
        "PATH", "HOME", "USER", "LOGNAME", "SHELL", "LANG", "LANGUAGE", "TERM", "TZ",
        "TMPDIR", "TMP", "TEMP",
        // Shared-library and interpreter discovery (actions/setup-python builds are --enable-shared)
        "LD_LIBRARY_PATH", "pythonLocation", "PKG_CONFIG_PATH",
        // Proxies and CA bundles, so installs keep working behind a corporate proxy
        "HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY", "SSL_CERT_FILE", "SSL_CERT_DIR",
        "REQUESTS_CA_BUNDLE", "NODE_EXTRA_CA_CERTS",
        // Telemetry opt-outs
        "NEXT_TELEMETRY_DISABLED", "DO_NOT_TRACK",
        // Windows (dev machines and the local compile gates)
        "SystemRoot", "SystemDrive", "windir", "ComSpec", "PATHEXT", "USERPROFILE", "APPDATA",
        "LOCALAPPDATA", "ProgramData", "ProgramFiles", "ProgramFiles(x86)", "ProgramW6432",
        "CommonProgramFiles", "CommonProgramFiles(x86)", "CommonProgramW6432", "HOMEDRIVE",
        "HOMEPATH", "USERNAME", "USERDOMAIN", "COMPUTERNAME", "NUMBER_OF_PROCESSORS",
        "PROCESSOR_ARCHITECTURE", "PROCESSOR_IDENTIFIER", "OS",
    };

    /// <summary>Prefixes (case-insensitive) of toolchain configuration families.</summary>
    private static readonly string[] AllowedPrefixes =
    [
        "LC_", "XDG_", "DOTNET_", "NUGET_", "NPM_CONFIG_", "PIP_",
    ];

    /// <summary>True when a variable of this name may be passed to a build child.</summary>
    public static bool IsAllowed(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        if (AllowedNames.Contains(name))
            return true;
        foreach (var prefix in AllowedPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>The allowlisted subset of <paramref name="source"/>.</summary>
    public static Dictionary<string, string> Filter(IDictionary source)
    {
        var filtered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in source)
        {
            if (entry.Key is string name && entry.Value is string value && IsAllowed(name))
                filtered[name] = value;
        }
        return filtered;
    }

    /// <summary>
    /// Replaces <paramref name="target"/> (a <c>ProcessStartInfo.Environment</c>, pre-populated
    /// from the current process) with the allowlisted subset of the current process environment.
    /// </summary>
    public static void Apply(IDictionary<string, string?> target)
    {
        var allowed = Filter(Environment.GetEnvironmentVariables());
        target.Clear();
        foreach (var (name, value) in allowed)
            target[name] = value;
    }
}
