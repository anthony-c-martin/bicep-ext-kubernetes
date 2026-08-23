# Kubernetes Bicep Extension

A [Bicep extension](https://github.com/Azure/bicep/blob/main/docs/experimental/local-deploy.md) for deploying
Kubernetes objects, using a kubeconfig file to authenticate.

Every built-in Kubernetes API kind is supported. Types are vendored from
[Azure/bicep-types-k8s](https://github.com/Azure/bicep-types-k8s) (currently Kubernetes `v1.33.0`), so resources
are strongly typed, with IntelliSense and validation in VS Code:

```bicep
targetScope = 'local'

@secure()
param kubeConfig string

extension kubernetes with {
  kubeConfig: kubeConfig
  namespace: 'default'
}

resource deployment 'apps/Deployment@v1' = {
  metadata: {
    name: 'echo-server'
  }
  spec: {
    replicas: 1
    selector: {
      matchLabels: { app: 'echo-server' }
    }
    template: {
      metadata: {
        name: 'echo-server'
        labels: { app: 'echo-server' }
      }
      spec: {
        containers: [
          {
            name: 'echo-server'
            image: 'ealen/echo-server:0.8.12'
          }
        ]
      }
    }
  }
}
```

Custom resources backed by a CRD are also supported, via a fallback type that accepts any object body.

## Configuration

| Property | Required | Description |
| --- | --- | --- |
| `kubeConfig` | Yes | The contents of a kubeconfig file, base-64 encoded. |
| `namespace` | Yes | The default namespace to deploy resources to. |
| `context` | No | The kubeconfig context to use. Defaults to the current context in the kubeconfig file. |

## Usage

1. Download the [Samples folder](https://download-directory.github.io/?url=https%3A%2F%2Fgithub.com%2Fanthony-c-martin%2Fbicep-ext-kubernetes%2Ftree%2Fmain%2Fsamples), and unzip it.
1. Export your kubeconfig so that the sample parameter files can pick it up:
    ```sh
    export KUBECONFIG_BASE64=$(kubectl config view --raw --minify --flatten | base64)
    ```
1. Open the unzipped Samples folder in VSCode, and select one of the `.bicepparam` files you wish to deploy.
1. Launch the [Deploy Pane](https://github.com/Azure/bicep/blob/main/docs/experimental/deploy-ui.md) to run the deployment.

> [!NOTE]
> Extension binary packages are not signed on a Mac. If you see the following error, you will need to manually sign the extension package:
>
> `Failed to launch provider: Failed to connect to provider /Users/ant/.bicep/br/ghcr.io/anthony-c-martin$bicep-ext-kubernetes/0.1.4$/extension.bin`
>
> To work around it, run the following in a terminal window, using the path from the error message:
>
> `codesign -s - '/Users/ant/.bicep/br/ghcr.io/anthony-c-martin$bicep-ext-kubernetes/0.1.4$/extension.bin'`

## Samples

| Sample | Demonstrates |
| --- | --- |
| [echo-server](./samples/echo-server) | A minimal Deployment and Service. |
| [voting-app](./samples/voting-app) | A two-tier application with a Redis back end and a web front end. |
| [web-app](./samples/web-app) | Namespace, ConfigMap, Secret, ServiceAccount, Deployment, Service, HorizontalPodAutoscaler, PodDisruptionBudget and Ingress. |

## Build + Test Locally

Build the solution:

```sh
dotnet build .
dotnet test .
```

Publish the extension to the local file system, and point the samples at it:

```sh
./scripts/publish.ps1 ./bicep-ext-kubernetes
jq '.extensions.kubernetes="../bicep-ext-kubernetes"' ./samples/bicepconfig.json > ./samples/bicepconfig.new.json
mv ./samples/bicepconfig.new.json ./samples/bicepconfig.json
```

Check the samples for warnings and errors:

```sh
bicep lint --pattern './samples/**/*.bicepparam'
```

Run a deployment:

```sh
export KUBECONFIG_BASE64=$(kubectl config view --raw --minify --flatten | base64)
bicep local-deploy ./samples/echo-server/main.bicepparam
```

To enable verbose tracing, run the following beforehand:

```sh
export BICEP_TRACING_ENABLED=true
```

## Updating the Kubernetes type definitions

The Bicep type definitions under [`types`](./types) are vendored verbatim from
[Azure/bicep-types-k8s](https://github.com/Azure/bicep-types-k8s), embedded into the extension binary at build
time, and served at runtime by
[`EmbeddedTypeDefinitionBuilder`](./src/Bicep.Extension.Kubernetes/Types/EmbeddedTypeDefinitionBuilder.cs). The
only modification made at runtime is to replace the upstream configuration type with this extension's
kubeconfig-based [`Configuration`](./src/Bicep.Extension.Kubernetes/Models/Models.cs).

To move to a newer Kubernetes version, run:

```sh
./scripts/sync-types.ps1 -KubernetesVersion v1.33.0
```

## Releasing

Run the [Release workflow](../../actions/workflows/release.yml), supplying the version to publish (e.g. `0.2.0`).
It builds every supported platform, publishes the extension to `ghcr.io`, tags the commit and creates a GitHub
release.
