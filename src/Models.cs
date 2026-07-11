using System.Text.Json.Serialization;
using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Kubernetes;

public class Configuration
{
    [TypeProperty("The Kubernetes configuration file content", ObjectTypePropertyFlags.Required, isSecure: true)]
    public required string KubeConfig { get; set; }

    [TypeProperty("The Kubernetes namespace to use")]
    public string? Namespace { get; set; }
}
