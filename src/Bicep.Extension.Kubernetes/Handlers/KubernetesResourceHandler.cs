using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Deployments.Extensibility.Core.Exceptions;
using Azure.Deployments.Extensibility.Providers.Kubernetes;
using Bicep.Local.Extension.Host.Handlers;
using ExtCore = Azure.Deployments.Extensibility.Core;

namespace Bicep.Extension.Kubernetes.Handlers;

/// <summary>
/// Handles every Kubernetes resource type, including custom resources matched by the fallback type.
/// </summary>
/// <remarks>
/// Resource bodies are passed through as untyped JSON because the type definitions are vendored from
/// Azure/bicep-types-k8s rather than declared in C#. The actual API interactions are delegated to
/// <see cref="KubernetesProvider"/>.
/// </remarks>
public class KubernetesResourceHandler : GenericResourceHandler<Configuration>
{
    private const string ProviderName = "Kubernetes";
    private const string ProviderVersion = "1.0.0";

    private static readonly JsonSerializerOptions ConfigSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly KubernetesProvider provider = new();

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => InvokeAsync(request, (provider, operation) => provider.SaveAsync(operation, cancellationToken));

    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => InvokeAsync(request, (provider, operation) => provider.PreviewSaveAsync(operation, cancellationToken));

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => InvokeAsync(request, (provider, operation) => provider.GetAsync(operation, cancellationToken));

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => InvokeAsync(request, (provider, operation) => provider.DeleteAsync(operation, cancellationToken));

    protected override JsonObject GetIdentifiers(JsonObject properties)
        => ExtractIdentifiers(properties);

    /// <summary>
    /// Projects a resource body down to the properties that identify it: the object's name and namespace.
    /// </summary>
    internal static JsonObject ExtractIdentifiers(JsonObject properties)
    {
        var identifiers = new JsonObject();

        if (properties["metadata"] is JsonObject metadata)
        {
            var identifierMetadata = new JsonObject();

            if (metadata["name"] is { } name)
            {
                identifierMetadata["name"] = name.DeepClone();
            }

            if (metadata["namespace"] is { } @namespace)
            {
                identifierMetadata["namespace"] = @namespace.DeepClone();
            }

            identifiers["metadata"] = identifierMetadata;
        }

        return identifiers;
    }

    private Task<ResourceResponse> InvokeAsync(
        ResourceRequest request,
        Func<KubernetesProvider, ExtCore.ExtensibilityOperationRequest, Task<ExtCore.ExtensibilityOperationResponse>> operation)
        => InvokeAsync(request, request.Properties, operation);

    private Task<ResourceResponse> InvokeAsync(
        ReferenceRequest request,
        Func<KubernetesProvider, ExtCore.ExtensibilityOperationRequest, Task<ExtCore.ExtensibilityOperationResponse>> operation)
        => InvokeAsync(request, request.Identifiers, operation);

    private async Task<ResourceResponse> InvokeAsync(
        ResourceBase request,
        JsonObject body,
        Func<KubernetesProvider, ExtCore.ExtensibilityOperationRequest, Task<ExtCore.ExtensibilityOperationResponse>> operation)
    {
        var operationRequest = new ExtCore.ExtensibilityOperationRequest(
            new(ProviderName, ProviderVersion, JsonSerializer.SerializeToElement(request.Config, ConfigSerializerOptions)),
            new(FullyQualifiedType(request.Type, request.ApiVersion), JsonSerializer.SerializeToElement(body)));

        ExtCore.ExtensibilityOperationResponse response;
        try
        {
            response = await operation(provider, operationRequest);
        }
        catch (ExtensibilityException exception)
        {
            throw ToResourceErrorException(exception.Errors);
        }

        return ToResourceResponse(response, request.Type, request.ApiVersion);
    }

    private ResourceResponse ToResourceResponse(ExtCore.ExtensibilityOperationResponse response, string type, string? apiVersion)
    {
        switch (response)
        {
            case ExtCore.ExtensibilityOperationErrorResponse errorResponse:
                throw ToResourceErrorException(errorResponse.Errors);

            case ExtCore.ExtensibilityOperationSuccessResponse successResponse:
                var properties = JsonObject.Create(successResponse.Resource.Properties)
                    ?? throw new ResourceErrorException("InvalidResponse", "The Kubernetes provider returned a resource without a body.");

                return new ResourceResponse
                {
                    Type = type,
                    ApiVersion = apiVersion,
                    Properties = properties,
                    Identifiers = GetIdentifiers(properties),
                };

            default:
                throw new ResourceErrorException("InvalidResponse", $"The Kubernetes provider returned an unexpected response of type '{response.GetType().Name}'.");
        }
    }

    private static ResourceErrorException ToResourceErrorException(IEnumerable<ExtCore.ExtensibilityError> errors)
    {
        var errorList = errors.ToImmutableArray();

        if (errorList.Length == 1)
        {
            var error = errorList[0];
            return new ResourceErrorException(new Error
            {
                Code = error.Code,
                Target = error.Target.ToString(),
                Message = error.Message,
            });
        }

        return new ResourceErrorException(new Error
        {
            Code = "MultipleErrorsOccurred",
            Target = string.Empty,
            Message = "Multiple errors occurred",
            Details = [.. errorList.Select(x => new ErrorDetail
            {
                Code = x.Code,
                Target = x.Target.ToString(),
                Message = x.Message,
            })],
        });
    }

    private static string FullyQualifiedType(string type, string? apiVersion)
        => string.IsNullOrEmpty(apiVersion) ? type : $"{type}@{apiVersion}";
}
