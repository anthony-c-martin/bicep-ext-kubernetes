using System.Reflection;
using Bicep.Extension.Kubernetes;
using Bicep.Extension.Kubernetes.Handlers;
using Bicep.Extension.Kubernetes.Types;
using Bicep.Local.Extension.Builder.Models;
using Bicep.Local.Extension.Host.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var assembly = typeof(Program).Assembly;
var assemblyName = assembly.GetName().Name ?? "bicep-ext-kubernetes";
var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? assembly.GetName().Version?.ToString()
    ?? "0.0.0";

var extensionInfo = new ExtensionInfo(
    name: assemblyName.Split('-')[^1],
    version: informationalVersion.Split('+')[0],
    isSingleton: true);

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services
    .AddBicepExtension()
    .WithExtensionInfo(extensionInfo.Name, extensionInfo.Version, extensionInfo.IsSingleton)
    .WithTypeDefinitionBuilder(new EmbeddedTypeDefinitionBuilder(extensionInfo))
    .WithResourceHandler<KubernetesResourceHandler>();

var app = builder.Build();
app.MapBicepExtension();

await app.RunAsync();
