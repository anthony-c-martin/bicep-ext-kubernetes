targetScope = 'local'

@secure()
@description('The contents of a kubeconfig file, base-64 encoded.')
param kubeConfig string

@description('The namespace to deploy into. It must already exist.')
param namespace string = 'default'

@description('The number of echo server replicas to run.')
param replicas int = 1

extension kubernetes with {
  kubeConfig: kubeConfig
  namespace: namespace
}

var name = 'echo-server'
var image = 'ealen/echo-server:0.8.12'
var containerPort = 80
var servicePort = 8080

var labels = {
  app: name
}

@description('Echo server deployment')
resource deployment 'apps/Deployment@v1' = {
  metadata: {
    name: name
    labels: labels
  }
  spec: {
    replicas: replicas
    selector: {
      matchLabels: labels
    }
    template: {
      metadata: {
        name: name
        labels: labels
      }
      spec: {
        containers: [
          {
            name: name
            image: image
            ports: [
              {
                name: 'http'
                containerPort: containerPort
              }
            ]
            resources: {
              requests: {
                cpu: '50m'
                memory: '64Mi'
              }
              limits: {
                cpu: '200m'
                memory: '128Mi'
              }
            }
            readinessProbe: {
              httpGet: {
                path: '/'
                // 'port' is a Kubernetes IntOrString, which the type definitions model as a string, so
                // the named container port is referenced rather than its number.
                port: 'http'
              }
              initialDelaySeconds: 2
              periodSeconds: 5
            }
          }
        ]
      }
    }
  }
}

@description('Echo server service')
resource service 'core/Service@v1' = {
  metadata: {
    name: name
    labels: labels
  }
  spec: {
    type: 'ClusterIP'
    selector: labels
    ports: [
      {
        port: servicePort
        targetPort: 'http'
      }
    ]
  }
}

output deploymentName string = deployment.metadata.name
output serviceName string = service.metadata.name
