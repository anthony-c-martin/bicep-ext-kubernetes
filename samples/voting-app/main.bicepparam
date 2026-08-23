using 'main.bicep'

// Populate with: export KUBECONFIG_BASE64=$(kubectl config view --raw --minify --flatten | base64)
param kubeConfig = readEnvironmentVariable('KUBECONFIG_BASE64')
param namespace = 'default'
param frontEndServiceType = 'LoadBalancer'
