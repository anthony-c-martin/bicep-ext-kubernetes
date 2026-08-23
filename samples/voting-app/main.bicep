targetScope = 'local'

@secure()
@description('The contents of a kubeconfig file, base-64 encoded.')
param kubeConfig string

@description('The namespace to deploy into. It must already exist.')
param namespace string = 'default'

@description('The service type to expose the front end with. Use \'LoadBalancer\' on a cloud cluster.')
@allowed([
  'ClusterIP'
  'LoadBalancer'
  'NodePort'
])
param frontEndServiceType string = 'LoadBalancer'

extension kubernetes with {
  kubeConfig: kubeConfig
  namespace: namespace
}

var backName = 'azure-vote-back'
var backPort = 6379

var frontName = 'azure-vote-front'
var frontPort = 80

@description('Application back-end deployment (redis)')
resource backDeploy 'apps/Deployment@v1' = {
  metadata: {
    name: backName
  }
  spec: {
    replicas: 1
    selector: {
      matchLabels: {
        app: backName
      }
    }
    template: {
      metadata: {
        name: backName
        labels: {
          app: backName
        }
      }
      spec: {
        containers: [
          {
            name: backName
            image: 'redis:7'
            env: [
              {
                name: 'ALLOW_EMPTY_PASSWORD'
                value: 'yes'
              }
            ]
            resources: {
              requests: {
                cpu: '100m'
                memory: '128Mi'
              }
              limits: {
                cpu: '250m'
                memory: '256Mi'
              }
            }
            ports: [
              {
                containerPort: backPort
                name: 'redis'
              }
            ]
          }
        ]
      }
    }
  }
}

@description('Configure back-end service')
resource backSvc 'core/Service@v1' = {
  metadata: {
    name: backName
  }
  spec: {
    ports: [
      {
        port: backPort
      }
    ]
    selector: {
      app: backName
    }
  }
}

@description('Application front-end deployment (website)')
resource frontDeploy 'apps/Deployment@v1' = {
  metadata: {
    name: frontName
  }
  spec: {
    replicas: 1
    selector: {
      matchLabels: {
        app: frontName
      }
    }
    template: {
      metadata: {
        name: frontName
        labels: {
          app: frontName
        }
      }
      spec: {
        nodeSelector: {
          'kubernetes.io/os': 'linux'
        }
        containers: [
          {
            name: frontName
            image: 'neilpeterson/azure-vote-front:v3'
            resources: {
              requests: {
                cpu: '100m'
                memory: '128Mi'
              }
              limits: {
                cpu: '250m'
                memory: '256Mi'
              }
            }
            ports: [
              {
                containerPort: frontPort
              }
            ]
            env: [
              {
                name: 'REDIS'
                value: backName
              }
            ]
          }
        ]
      }
    }
  }
}

@description('Configure front-end service')
resource frontSvc 'core/Service@v1' = {
  metadata: {
    name: frontName
  }
  spec: {
    type: frontEndServiceType
    ports: [
      {
        port: frontPort
      }
    ]
    selector: {
      app: frontName
    }
  }
}

output frontEndServiceName string = frontSvc.metadata.name
