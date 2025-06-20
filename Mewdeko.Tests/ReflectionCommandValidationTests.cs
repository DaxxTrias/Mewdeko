using System.Reflection;
using Mewdeko.Common.Attributes.TextCommands;
using YamlDotNet.Serialization;

namespace Mewdeko.Tests;

/// <summary>
///     Simple reflection-based tests to validate command attributes and localization.
/// </summary>
[TestFixture]
public class ReflectionCommandValidationTests
{
    [OneTimeSetUp]
    public void SetUp()
    {
        var projectRoot = GetProjectRoot();
        LoadDataFiles(projectRoot);
        _commandMethods = GetAllCommandMethods();
    }

    private static Dictionary<string, string[]>? _aliasesData;
    private static Dictionary<string, object>? _commandStringsData;
    private static List<MethodInfo>? _commandMethods;

    /// <summary>
    ///     Every method with [Cmd] must also have [Aliases].
    /// </summary>
    [Test]
    public void EveryCommandMethodHasBothAttributes()
    {
        var failures = new List<string>();

        foreach (var method in _commandMethods!)
        {
            var hasCmd = method.GetCustomAttribute<Cmd>() != null;
            var hasAliases = method.GetCustomAttribute<AliasesAttribute>() != null;

            if (hasCmd && !hasAliases)
            {
                failures.Add($"Method '{method.DeclaringType?.Name}.{method.Name}' has [Cmd] but missing [Aliases]");
            }

            if (hasAliases && !hasCmd)
            {
                failures.Add($"Method '{method.DeclaringType?.Name}.{method.Name}' has [Aliases] but missing [Cmd]");
            }
        }

        Assert.That(failures, Is.Empty,
            "Found methods with mismatched attributes:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     Every command method must have an entry in aliases.yml.
    /// </summary>
    [Test]
    public void EveryCommandMethodHasAliasEntry()
    {
        var failures = new List<string>();

        foreach (var method in _commandMethods!.Where(m => m.GetCustomAttribute<Cmd>() != null))
        {
            var methodName = method.Name.ToLowerInvariant();

            if (!_aliasesData!.ContainsKey(methodName))
            {
                failures.Add($"Method '{method.DeclaringType?.Name}.{method.Name}' missing from aliases.yml");
            }
            else if (_aliasesData[methodName].Length == 0)
            {
                failures.Add($"Method '{method.DeclaringType?.Name}.{method.Name}' has empty aliases in aliases.yml");
            }
        }

        Assert.That(failures, Is.Empty,
            "Found command methods missing from aliases.yml:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     Every command must have localization strings.
    /// </summary>
    [Test]
    public void EveryCommandHasLocalizationStrings()
    {
        var failures = new List<string>();

        foreach (var method in _commandMethods!.Where(m => m.GetCustomAttribute<Cmd>() != null))
        {
            var methodName = method.Name.ToLowerInvariant();

            if (!_aliasesData!.TryGetValue(methodName, out var aliases) || aliases.Length == 0)
                continue; // Will be caught by previous test

            var primaryCommand = aliases[0];

            if (!_commandStringsData!.ContainsKey(primaryCommand))
            {
                failures.Add(
                    $"Command '{primaryCommand}' (method: {method.DeclaringType?.Name}.{method.Name}) missing from commands.en-US.yml");
            }
            else if (_commandStringsData[primaryCommand] is Dictionary<object, object> cmdData)
            {
                if (!cmdData.ContainsKey("desc") || string.IsNullOrWhiteSpace(cmdData["desc"]?.ToString()))
                {
                    failures.Add(
                        $"Command '{primaryCommand}' (method: {method.DeclaringType?.Name}.{method.Name}) missing or empty description");
                }
            }
        }

        Assert.That(failures, Is.Empty,
            "Found commands missing localization:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     Every entry in aliases.yml should have a corresponding command method.
    /// </summary>
    [Test]
    public void EveryAliasEntryHasCommandMethod()
    {
        var failures = new List<string>();
        var methodNames = _commandMethods!
            .Where(m => m.GetCustomAttribute<Cmd>() != null)
            .Select(m => m.Name.ToLowerInvariant())
            .ToHashSet();

        foreach (var aliasKey in _aliasesData!.Keys)
        {
            if (!methodNames.Contains(aliasKey))
            {
                failures.Add($"Alias entry '{aliasKey}' in aliases.yml has no corresponding command method");
            }
        }

        Assert.That(failures, Is.Empty,
            "Found orphaned alias entries:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     No duplicate primary command names in aliases.yml.
    /// </summary>
    [Test]
    public void NoDuplicatePrimaryCommandNames()
    {
        var failures = new List<string>();
        var primaryCommands = new Dictionary<string, List<string>>();

        foreach (var (aliasKey, aliases) in _aliasesData!)
        {
            if (aliases.Length == 0) continue;

            var primaryCommand = aliases[0];
            primaryCommands.TryAdd(primaryCommand, []);
            primaryCommands[primaryCommand].Add(aliasKey);
        }

        foreach (var (primaryCommand, methods) in primaryCommands.Where(kvp => kvp.Value.Count > 1))
        {
            failures.Add($"Primary command '{primaryCommand}' used by multiple methods: {string.Join(", ", methods)}");
        }

        Assert.That(failures, Is.Empty,
            "Found duplicate primary command names:\n" + string.Join("\n", failures));
    }

    /// <summary>
    ///     Get all methods with command attributes using reflection.
    /// </summary>
    private static List<MethodInfo> GetAllCommandMethods()
    {
        var methods = new List<MethodInfo>();

        // Try to get the Mewdeko assembly from loaded assemblies
        var mewdekoAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Mewdeko");

        if (mewdekoAssembly == null)
        {
            // Try to load it from a type we know exists
            try
            {
                mewdekoAssembly = typeof(Cmd).Assembly;
            }
            catch
            {
                throw new InvalidOperationException(
                    "Could not find Mewdeko assembly. Make sure the project is referenced and built.");
            }
        }

        // Find all types in the Modules namespace
        try
        {
            var moduleTypes = mewdekoAssembly.GetTypes()
                .Where(t => t.Namespace?.StartsWith("Mewdeko.Modules") == true);

            foreach (var type in moduleTypes)
            {
                var typeMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<Cmd>() != null ||
                                m.GetCustomAttribute<AliasesAttribute>() != null);

                methods.AddRange(typeMethods);
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            // If we can't load all types, work with what we can
            var loadedTypes = ex.Types.Where(t => t != null && t.Namespace?.StartsWith("Mewdeko.Modules") == true);

            foreach (var type in loadedTypes)
            {
                try
                {
                    var typeMethods = type!.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Where(m => m.GetCustomAttribute<Cmd>() != null ||
                                    m.GetCustomAttribute<AliasesAttribute>() != null);

                    methods.AddRange(typeMethods);
                }
                catch
                {
                    // Skip types that can't be inspected
                }
            }
        }

        return methods;
    }

    private static void LoadDataFiles(string projectRoot)
    {
        // Load aliases.yml
        var aliasesPath = Path.Combine(projectRoot, "data", "aliases.yml");
        var aliasesYaml = File.ReadAllText(aliasesPath);
        _aliasesData = new Deserializer().Deserialize<Dictionary<string, string[]>>(aliasesYaml);

        // Load commands.en-US.yml
        var commandStringsPath = Path.Combine(projectRoot, "data", "strings", "commands", "commands.en-US.yml");
        var commandStringsYaml = File.ReadAllText(commandStringsPath);
        _commandStringsData = new Deserializer().Deserialize<Dictionary<string, object>>(commandStringsYaml);
    }

    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(currentDir, "src", "Mewdeko", "Modules")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName
                         ?? throw new DirectoryNotFoundException("Could not find project root");
        }

        return Path.Combine(currentDir, "src", "Mewdeko");
    }
}