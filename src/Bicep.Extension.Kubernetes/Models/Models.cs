using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Kubernetes.Models;

/// <summary>
/// The <c>extension kubernetes with { ... }</c> configuration. This deliberately mirrors
/// <see cref="Azure.Deployments.Extensibility.Providers.Kubernetes.Models.KubernetesConfig"/>, which is the
/// shape the underlying Kubernetes provider deserializes.
/// </summary>
public class Configuration
{
    [TypeProperty("The default Kubernetes namespace to deploy resources to.", ObjectTypePropertyFlags.Required)]
    public required string Namespace { get; set; }

    [TypeProperty("The Kubernetes configuration file, base-64 encoded.", ObjectTypePropertyFlags.Required, isSecure: true)]
    public required string KubeConfig { get; set; }

    [TypeProperty("The kubeconfig context to use. If not set, the current context within the kubeconfig file is used.")]
    public string? Context { get; set; }
}
