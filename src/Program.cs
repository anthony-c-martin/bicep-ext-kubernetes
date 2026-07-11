using Microsoft.AspNetCore.Builder;
using Bicep.Local.Extension.Host.Extensions;
using Bicep.Extension.Kubernetes.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Bicep.Extension.Kubernetes;

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services
    .AddBicepExtension()
    .WithDefaults(
        name: ThisAssembly.AssemblyName.Split('-')[^1],
        version: ThisAssembly.AssemblyInformationalVersion.Split('+')[0],
        isSingleton: true)
    .WithConfigurationType(typeof(Configuration))
    .WithTypeAssembly(typeof(Program).Assembly)
    .WithResourceHandler<KubernetesResourceHandler>();

var app = builder.Build();
app.MapBicepExtension();

await app.RunAsync();