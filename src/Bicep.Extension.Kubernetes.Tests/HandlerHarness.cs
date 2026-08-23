using System.Text;
using System.Text.Json;
using Bicep.Local.Extension.Host.Handlers;
using Bicep.Local.Rpc;

namespace Bicep.Extension.Kubernetes.Tests;

/// <summary>
/// Helpers for invoking resource handlers through their public <see cref="IResourceHandler"/> entry point,
/// mirroring how the Bicep local-deploy host calls them (JSON in, JSON out).
/// </summary>
public static class HandlerHarness
{
    // The Bicep host exchanges properties/config as camelCase JSON.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A syntactically valid kubeconfig pointing at an unroutable address, for exercising code paths that must
    /// not depend on a reachable cluster.
    /// </summary>
    public const string UnreachableKubeConfig = """
        apiVersion: v1
        kind: Config
        clusters:
        - name: test
          cluster:
            server: https://127.0.0.1:1
        contexts:
        - name: test
          context:
            cluster: test
            user: test
        current-context: test
        users:
        - name: test
          user:
            token: test-token
        """;

    public static string Config(string? kubeConfig = null, string @namespace = "default", string? context = null)
        => JsonSerializer.Serialize(
            new
            {
                @namespace,
                kubeConfig = Convert.ToBase64String(Encoding.UTF8.GetBytes(kubeConfig ?? UnreachableKubeConfig)),
                context,
            },
            SerializerOptions);

    public static Task<LocalExtensibilityOperationResponse> CreateOrUpdateAsync(
        IResourceHandler handler,
        string type,
        string apiVersion,
        object properties,
        string? config = null,
        CancellationToken cancellationToken = default)
        => handler.CreateOrUpdate(Specification(type, apiVersion, properties, config), cancellationToken);

    public static Task<LocalExtensibilityOperationResponse> PreviewAsync(
        IResourceHandler handler,
        string type,
        string apiVersion,
        object properties,
        string? config = null,
        CancellationToken cancellationToken = default)
        => handler.Preview(Specification(type, apiVersion, properties, config), cancellationToken);

    public static Task<LocalExtensibilityOperationResponse> GetAsync(
        IResourceHandler handler,
        string type,
        string apiVersion,
        object identifiers,
        string? config = null,
        CancellationToken cancellationToken = default)
        => handler.Get(Reference(type, apiVersion, identifiers, config), cancellationToken);

    public static Task<LocalExtensibilityOperationResponse> DeleteAsync(
        IResourceHandler handler,
        string type,
        string apiVersion,
        object identifiers,
        string? config = null,
        CancellationToken cancellationToken = default)
        => handler.Delete(Reference(type, apiVersion, identifiers, config), cancellationToken);

    public static JsonElement ResourceProperties(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNull(response.ErrorData);
        Assert.IsNotNull(response.Resource);
        return JsonSerializer.Deserialize<JsonElement>(response.Resource.Properties);
    }

    public static JsonElement ResourceIdentifiers(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNull(response.ErrorData);
        Assert.IsNotNull(response.Resource);
        return JsonSerializer.Deserialize<JsonElement>(response.Resource.Identifiers);
    }

    private static ResourceSpecification Specification(string type, string apiVersion, object properties, string? config)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Config = config ?? Config(),
            Properties = JsonSerializer.Serialize(properties, SerializerOptions),
        };

    private static ResourceReference Reference(string type, string apiVersion, object identifiers, string? config)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Config = config ?? Config(),
            Identifiers = JsonSerializer.Serialize(identifiers, SerializerOptions),
        };
}
