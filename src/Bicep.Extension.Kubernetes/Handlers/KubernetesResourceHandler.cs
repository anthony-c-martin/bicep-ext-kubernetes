// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Immutable;
using Azure.Deployments.Extensibility.Providers.Kubernetes;
using Bicep.Local.Extension.Protocol;
using System.Diagnostics;
using Azure.Deployments.Extensibility.Core.Json;
using System.Text.Json.Nodes;
using System.Text.Json;
using ExtCore = Azure.Deployments.Extensibility.Core;
using Json.More;

namespace Bicep.Extension.Kubernetes.Handlers;

public partial class KubernetesResourceHandler : IGenericResourceHandler
{
    public async Task<LocalExtensibilityOperationResponse> Delete(ResourceReference request, CancellationToken cancellationToken)
        => Convert(await new KubernetesProvider().DeleteAsync(Convert(request), cancellationToken), request.Type, request.ApiVersion);

    public async Task<LocalExtensibilityOperationResponse> Get(ResourceReference request, CancellationToken cancellationToken)
        => Convert(await new KubernetesProvider().GetAsync(Convert(request), cancellationToken), request.Type, request.ApiVersion);

    public async Task<LocalExtensibilityOperationResponse> Preview(ResourceSpecification request, CancellationToken cancellationToken)
        => Convert(await new KubernetesProvider().PreviewSaveAsync(Convert(request), cancellationToken), request.Type, request.ApiVersion);

    public async Task<LocalExtensibilityOperationResponse> CreateOrUpdate(ResourceSpecification request, CancellationToken cancellationToken)
        => Convert(await new KubernetesProvider().SaveAsync(Convert(request), cancellationToken), request.Type, request.ApiVersion);

    private static ExtCore.ExtensibilityOperationRequest Convert(ResourceSpecification request)
    {
        return new(
            new("Kubernetes", "1.0.0", JsonSerializer.Deserialize<JsonElement>(request.Config)),
            new(string.IsNullOrEmpty(request.ApiVersion) ? request.Type : $"{request.Type}@{request.ApiVersion}", JsonSerializer.Deserialize<JsonElement>(request.Properties)));
    }

    private static ExtCore.ExtensibilityOperationRequest Convert(ResourceReference request)
    {
        return new(
            new("Kubernetes", "1.0.0", JsonSerializer.Deserialize<JsonElement>(request.Config)),
            new(string.IsNullOrEmpty(request.ApiVersion) ? request.Type : $"{request.Type}@{request.ApiVersion}", JsonSerializer.Deserialize<JsonElement>(request.Identifiers)));
    }

    private static LocalExtensibilityOperationResponse Convert(ExtCore.ExtensibilityOperationResponse response, string type, string? apiVersion)
    {
        switch (response)
        {
            case ExtCore.ExtensibilityOperationErrorResponse errorResponse:
                var errors = errorResponse.Errors.ToArray();
                if (errors.Length > 1)
                {
                    return new(
                        null,
                        new(new("MultipleErrorsOccurred", "", "Multiple errors occurred", errorResponse.Errors.Select(x => new ErrorDetail(x.Code, x.Target.ToString(), x.Message)).ToArray(), null)));
                }
                
                var error = errors.First();
                return new(
                    null,
                    new(new(error.Code, error.Target.ToString(), error.Message, null, null)));
            case ExtCore.ExtensibilityOperationSuccessResponse successResponse:
                return new(
                    new(type, apiVersion, "Succeeded", JsonObject.Create(successResponse.Resource.Properties)!, null, JsonObject.Create(successResponse.Resource.Properties)!),
                    null);
            default:
                throw new InvalidOperationException($"Unexpected response type: {response.GetType()}");
        }
    }
}