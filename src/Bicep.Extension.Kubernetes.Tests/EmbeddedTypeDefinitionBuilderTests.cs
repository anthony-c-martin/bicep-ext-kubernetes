using System.Text.Json.Nodes;
using Bicep.Extension.Kubernetes.Types;
using Bicep.Local.Extension.Builder.Models;

namespace Bicep.Extension.Kubernetes.Tests;

[TestClass]
public class EmbeddedTypeDefinitionBuilderTests
{
    private static readonly ExtensionInfo ExtensionInfo = new("kubernetes", "1.2.3", isSingleton: true);

    private static (JsonObject Index, IReadOnlyDictionary<string, string> Files) Generate()
    {
        var definition = new EmbeddedTypeDefinitionBuilder(ExtensionInfo).GenerateTypeDefinition();
        var index = JsonNode.Parse(definition.IndexFileContent) as JsonObject;

        Assert.IsNotNull(index);
        return (index, definition.TypeFileContents);
    }

    [TestMethod]
    public void GenerateTypeDefinition_advertises_the_supplied_extension_info()
    {
        var (index, _) = Generate();
        var settings = index["settings"] as JsonObject;

        Assert.IsNotNull(settings);
        Assert.AreEqual("kubernetes", settings["name"]!.GetValue<string>());
        Assert.AreEqual("1.2.3", settings["version"]!.GetValue<string>());
        Assert.IsTrue(settings["isSingleton"]!.GetValue<bool>());
    }

    [TestMethod]
    public void GenerateTypeDefinition_replaces_the_upstream_configuration_type()
    {
        var (index, files) = Generate();

        var reference = index["settings"]!["configurationType"]!["$ref"]!.GetValue<string>();
        StringAssert.StartsWith(reference, "config.json#/");

        var configFile = files["config.json"];
        var configIndex = int.Parse(reference["config.json#/".Length..]);
        var configType = JsonNode.Parse(configFile)!.AsArray()[configIndex]!.AsObject();

        Assert.AreEqual("Configuration", configType["name"]!.GetValue<string>());

        var properties = configType["properties"]!.AsObject();
        CollectionAssert.AreEquivalent(
            new[] { "namespace", "kubeConfig", "context" },
            properties.Select(x => x.Key).ToArray());
    }

    [TestMethod]
    public void GenerateTypeDefinition_serves_the_vendored_resource_types()
    {
        var (index, files) = Generate();
        var resources = index["resources"]!.AsObject();

        // The vendored types cover every built-in Kubernetes API kind; spot-check the common ones.
        foreach (var expected in new[]
        {
            "apps/Deployment@v1",
            "apps/StatefulSet@v1",
            "batch/CronJob@v1",
            "core/ConfigMap@v1",
            "core/Secret@v1",
            "core/Service@v1",
            "networking.k8s.io/Ingress@v1",
            "rbac.authorization.k8s.io/ClusterRole@v1",
        })
        {
            Assert.IsTrue(resources.ContainsKey(expected), $"Expected resource type '{expected}' to be declared.");
        }

        Assert.IsTrue(resources.Count > 50, $"Expected a full set of resource types, but found {resources.Count}.");

        // Every resource must reference a type file that is actually served.
        foreach (var (name, node) in resources)
        {
            var reference = node!["$ref"]!.GetValue<string>();
            var fileName = reference[..reference.LastIndexOf("#/", StringComparison.Ordinal)];

            Assert.IsTrue(files.ContainsKey(fileName), $"Resource type '{name}' references missing type file '{fileName}'.");
        }
    }

    [TestMethod]
    public void GenerateTypeDefinition_serves_the_fallback_type_for_custom_resources()
    {
        var (index, files) = Generate();

        var reference = index["fallbackResourceType"]!["$ref"]!.GetValue<string>();
        var fileName = reference[..reference.LastIndexOf("#/", StringComparison.Ordinal)];

        Assert.IsTrue(files.ContainsKey(fileName), $"The fallback type references missing type file '{fileName}'.");
    }
}
