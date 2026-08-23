using System.Text.Json.Nodes;
using Bicep.Extension.Kubernetes.Handlers;

namespace Bicep.Extension.Kubernetes.Tests;

[TestClass]
public class KubernetesResourceHandlerTests
{
    private static readonly object DeploymentProperties = new
    {
        metadata = new
        {
            name = "echo-server",
            @namespace = "demo",
            labels = new Dictionary<string, string> { ["app"] = "echo-server" },
        },
        spec = new
        {
            replicas = 1,
            selector = new { matchLabels = new Dictionary<string, string> { ["app"] = "echo-server" } },
        },
    };

    [TestMethod]
    public void Handler_is_registered_as_the_generic_handler()
    {
        var handler = new KubernetesResourceHandler();

        // A null type/apiVersion registers the handler as the fallback for every resource type, which is what
        // allows a single handler to serve all of the vendored Kubernetes types plus custom resources.
        Assert.IsNull(handler.Type);
        Assert.IsNull(handler.ApiVersion);
    }

    [TestMethod]
    public void ExtractIdentifiers_projects_the_name_and_namespace()
    {
        var properties = JsonNode.Parse("""
            {
              "metadata": {
                "name": "echo-server",
                "namespace": "demo",
                "labels": { "app": "echo-server" }
              },
              "spec": { "replicas": 1 }
            }
            """)!.AsObject();

        var identifiers = KubernetesResourceHandler.ExtractIdentifiers(properties);
        var metadata = identifiers["metadata"]!.AsObject();

        Assert.AreEqual("echo-server", metadata["name"]!.GetValue<string>());
        Assert.AreEqual("demo", metadata["namespace"]!.GetValue<string>());

        // Only the identifying metadata is surfaced - labels and spec must not leak into the identifiers.
        CollectionAssert.AreEquivalent(new[] { "name", "namespace" }, metadata.Select(x => x.Key).ToArray());
        CollectionAssert.AreEquivalent(new[] { "metadata" }, identifiers.Select(x => x.Key).ToArray());
    }

    [TestMethod]
    public void ExtractIdentifiers_omits_the_namespace_for_cluster_scoped_resources()
    {
        var properties = JsonNode.Parse("""{ "metadata": { "name": "cluster-admin" } }""")!.AsObject();

        var metadata = KubernetesResourceHandler.ExtractIdentifiers(properties)["metadata"]!.AsObject();

        Assert.AreEqual("cluster-admin", metadata["name"]!.GetValue<string>());
        Assert.IsFalse(metadata.ContainsKey("namespace"));
    }

    [TestMethod]
    public void ExtractIdentifiers_tolerates_a_body_without_metadata()
    {
        var properties = JsonNode.Parse("""{ "spec": { "replicas": 1 } }""")!.AsObject();

        Assert.AreEqual(0, KubernetesResourceHandler.ExtractIdentifiers(properties).Count);
    }

    [TestMethod]
    public void ExtractIdentifiers_does_not_alias_the_source_document()
    {
        var properties = JsonNode.Parse("""{ "metadata": { "name": "echo-server" } }""")!.AsObject();

        var identifiers = KubernetesResourceHandler.ExtractIdentifiers(properties);
        identifiers["metadata"]!["name"] = "mutated";

        Assert.AreEqual("echo-server", properties["metadata"]!["name"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task Preview_reports_validation_failures_as_errors()
    {
        var handler = new KubernetesResourceHandler();

        // 'metadata.name' is required by every Kubernetes object, and is rejected before any API call is made.
        var response = await HandlerHarness.PreviewAsync(handler, "apps/Deployment", "v1", new { spec = new { replicas = 1 } });

        Assert.IsNotNull(response.ErrorData);
        Assert.IsNull(response.Resource);
    }

    [TestMethod]
    public async Task Preview_reports_unparsable_configuration_as_an_error()
    {
        var handler = new KubernetesResourceHandler();

        var response = await HandlerHarness.PreviewAsync(
            handler,
            "apps/Deployment",
            "v1",
            DeploymentProperties,
            HandlerHarness.Config(kubeConfig: "this is not a kubeconfig"));

        Assert.IsNotNull(response.ErrorData);
        Assert.IsNull(response.Resource);
    }

    [TestMethod]
    public async Task Get_surfaces_connection_failures_as_errors()
    {
        var handler = new KubernetesResourceHandler();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // The kubeconfig points at an unroutable address, so this must fail cleanly rather than throw.
        var response = await HandlerHarness.GetAsync(
            handler,
            "apps/Deployment",
            "v1",
            new { metadata = new { name = "echo-server", @namespace = "demo" } },
            cancellationToken: cts.Token);

        Assert.IsNotNull(response.ErrorData);
        Assert.IsNull(response.Resource);
    }
}
