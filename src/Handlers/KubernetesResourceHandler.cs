using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Deployments.Extensibility.Core.Exceptions;
using Azure.Deployments.Extensibility.Providers.Kubernetes;
using Bicep.Local.Extension.Host.Handlers;
using ExtCore = Azure.Deployments.Extensibility.Core;

namespace Bicep.Extension.Kubernetes.Handlers;

public class KubernetesResourceHandler : GenericResourceHandler<Configuration>
{
    private static readonly JsonSerializerOptions ConfigSerializerOptions = new(JsonSerializerDefaults.Web);

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => WrapExceptions(async () => Convert(await new KubernetesProvider().DeleteAsync(Convert(request), cancellationToken), request.Type, request.ApiVersion));

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => WrapExceptions(async () => Convert(await new KubernetesProvider().GetAsync(Convert(request), cancellationToken), request.Type, request.ApiVersion));

    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => WrapExceptions(async () => Convert(await new KubernetesProvider().PreviewSaveAsync(Convert(request), cancellationToken), request.Type, request.ApiVersion));

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => WrapExceptions(async () => Convert(await new KubernetesProvider().SaveAsync(Convert(request), cancellationToken), request.Type, request.ApiVersion));

    private static ExtCore.ExtensibilityOperationRequest Convert(ResourceRequest request)
        => new(
            new("Kubernetes", "1.0.0", JsonSerializer.SerializeToElement(request.Config, ConfigSerializerOptions)),
            new(FullyQualifiedType(request.Type, request.ApiVersion), JsonSerializer.SerializeToElement(request.Properties)));

    private static ExtCore.ExtensibilityOperationRequest Convert(ReferenceRequest request)
        => new(
            new("Kubernetes", "1.0.0", JsonSerializer.SerializeToElement(request.Config, ConfigSerializerOptions)),
            new(FullyQualifiedType(request.Type, request.ApiVersion), JsonSerializer.SerializeToElement(request.Identifiers)));

    private static string FullyQualifiedType(string type, string? apiVersion)
        => string.IsNullOrEmpty(apiVersion) ? type : $"{type}@{apiVersion}";

    private ResourceResponse Convert(ExtCore.ExtensibilityOperationResponse response, string type, string? apiVersion)
    {
        switch (response)
        {
            case ExtCore.ExtensibilityOperationErrorResponse errorResponse:
                var errors = errorResponse.Errors.ToArray();
                if (errors.Length > 1)
                {
                    throw new ResourceErrorException(new Error
                    {
                        Code = "MultipleErrorsOccurred",
                        Target = "",
                        Message = "Multiple errors occurred",
                        Details = [.. errors.Select(x => new ErrorDetail
                        {
                            Code = x.Code,
                            Target = x.Target.ToString(),
                            Message = x.Message,
                        })],
                    });
                }
                
                var error = errors.First();
                throw new ResourceErrorException(new Error
                {
                    Code = error.Code,
                    Target = error.Target.ToString(),
                    Message = error.Message,
                });
            case ExtCore.ExtensibilityOperationSuccessResponse successResponse:
                var properties = JsonObject.Create(successResponse.Resource.Properties)!;
                return new ResourceResponse
                {
                    Type = type,
                    ApiVersion = apiVersion,
                    Properties = properties,
                    Identifiers = GetIdentifiers(properties),
                };
            default:
                throw new InvalidOperationException($"Unexpected response type: {response.GetType()}");
        }
    }

    private static async Task<ResourceResponse> WrapExceptions(Func<Task<ResourceResponse>> func)
    {
        try
        {
            return await func();
        }
        catch (ExtensibilityException ex) when (ex.Errors.Count() == 1)
        {
            var error = ex.Errors.First();
            throw new ResourceErrorException(new Error
            {
                Code = error.Code,
                Target = error.Target.ToString(),
                Message = error.Message,
            });
        }
        catch (ExtensibilityException ex)
        {
            throw new ResourceErrorException(new Error
            {
                Code = "MultipleErrorsOccurred",
                Target = "",
                Message = "Multiple errors occurred",
                Details = [.. ex.Errors.Select(x => new ErrorDetail
                {
                    Code = x.Code,
                    Target = x.Target.ToString(),
                    Message = x.Message,
                })],
            });
        }
    }

    protected override JsonObject GetIdentifiers(JsonObject properties)
    {
        var identifiers = new JsonObject();

        if (properties["apiVersion"] is { } apiVersion)
        {
            identifiers["apiVersion"] = apiVersion.DeepClone();
        }

        if (properties["kind"] is { } kind)
        {
            identifiers["kind"] = kind.DeepClone();
        }

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
}