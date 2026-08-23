using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bicep.Local.Extension.Builder.Models;
using Bicep.Local.Extension.Types;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Kubernetes.Types;

/// <summary>
/// Serves the Bicep type definitions vendored verbatim from
/// <see href="https://github.com/Azure/bicep-types-k8s">Azure/bicep-types-k8s</see>, which are embedded into the
/// assembly at build time.
/// </summary>
/// <remarks>
/// The upstream type index declares a configuration type modelled on the Azure Deployments Kubernetes extension
/// (an AKS-aware <c>Managed</c>/<c>Custom</c> discriminated union). This extension authenticates purely with a
/// kubeconfig file, so the index is rewritten to reference a configuration type generated from
/// <see cref="Models.Configuration"/> instead. Resource types and the fallback type are served unmodified.
/// </remarks>
public class EmbeddedTypeDefinitionBuilder : ITypeDefinitionBuilder
{
    private const string ResourcePrefix = "types/";
    private const string IndexFileName = "index.json";
    private const string ConfigFileName = "config.json";

    private readonly ExtensionInfo extensionInfo;
    private readonly Assembly assembly;

    public EmbeddedTypeDefinitionBuilder(ExtensionInfo extensionInfo)
        : this(extensionInfo, typeof(EmbeddedTypeDefinitionBuilder).Assembly)
    {
    }

    internal EmbeddedTypeDefinitionBuilder(ExtensionInfo extensionInfo, Assembly assembly)
    {
        this.extensionInfo = extensionInfo;
        this.assembly = assembly;
    }

    public TypeDefinition GenerateTypeDefinition()
    {
        var embeddedFiles = ReadEmbeddedTypeFiles();

        if (!embeddedFiles.TryGetValue(IndexFileName, out var indexContent))
        {
            throw new InvalidOperationException(
                $"The embedded type index '{ResourcePrefix}{IndexFileName}' was not found. Run './scripts/sync-types.ps1' to populate the 'types' directory.");
        }

        var (configFileContent, configTypeIndex) = GenerateConfigurationTypeFile();

        var typeFiles = embeddedFiles
            .Where(x => x.Key != IndexFileName)
            .Append(new(ConfigFileName, configFileContent))
            .ToImmutableDictionary();

        return new TypeDefinition(
            IndexFileContent: RewriteIndex(indexContent, configTypeIndex),
            TypeFileContents: typeFiles);
    }

    /// <summary>
    /// Rewrites the vendored index so that it advertises this extension's name and version, and points at the
    /// configuration type generated from <see cref="Models.Configuration"/>.
    /// </summary>
    private string RewriteIndex(string indexContent, int configTypeIndex)
    {
        var index = JsonNode.Parse(indexContent) as JsonObject
            ?? throw new InvalidOperationException($"The embedded type index '{ResourcePrefix}{IndexFileName}' is not a JSON object.");

        var settings = index["settings"] as JsonObject
            ?? throw new InvalidOperationException($"The embedded type index '{ResourcePrefix}{IndexFileName}' does not declare 'settings'.");

        settings["name"] = extensionInfo.Name;
        settings["version"] = extensionInfo.Version;
        settings["isSingleton"] = extensionInfo.IsSingleton;
        settings["configurationType"] = new JsonObject
        {
            ["$ref"] = $"{ConfigFileName}#/{configTypeIndex}",
        };

        return index.ToJsonString();
    }

    /// <summary>
    /// Generates a type file containing only the configuration type, and returns it alongside the index of that
    /// type within the file.
    /// </summary>
    private (string Content, int TypeIndex) GenerateConfigurationTypeFile()
    {
        var definition = new TypeDefinitionBuilder(extensionInfo, new ConfigurationOnlyTypeProvider()).GenerateTypeDefinition();

        var generatedIndex = JsonNode.Parse(definition.IndexFileContent) as JsonObject;
        var reference = generatedIndex?["settings"]?["configurationType"]?["$ref"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Failed to generate a configuration type reference.");

        // The generated reference is of the form 'types.json#/<index>'.
        var separatorIndex = reference.LastIndexOf("#/", StringComparison.Ordinal);
        if (separatorIndex < 0 ||
            !int.TryParse(reference[(separatorIndex + 2)..], out var typeIndex))
        {
            throw new InvalidOperationException($"Failed to parse the generated configuration type reference '{reference}'.");
        }

        var fileName = reference[..separatorIndex];
        if (!definition.TypeFileContents.TryGetValue(fileName, out var content))
        {
            throw new InvalidOperationException($"The generated configuration type references missing file '{fileName}'.");
        }

        return (content, typeIndex);
    }

    private ImmutableDictionary<string, string> ReadEmbeddedTypeFiles()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Failed to read embedded resource '{resourceName}'.");
            using var reader = new StreamReader(stream);

            // MSBuild emits '\' path separators for nested resources on Windows, but Bicep type references
            // always use '/'.
            var path = resourceName[ResourcePrefix.Length..].Replace('\\', '/');

            builder.Add(path, reader.ReadToEnd());
        }

        return builder.ToImmutable();
    }

    private sealed class ConfigurationOnlyTypeProvider : ITypeProvider
    {
        public Type? ConfigurationType => typeof(Models.Configuration);

        public Type? FallbackType => null;

        public IEnumerable<(Type type, ResourceTypeAttribute attribute)> GetResourceTypes(bool throwOnDuplicate) => [];
    }
}
