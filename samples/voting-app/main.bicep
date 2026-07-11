targetScope = 'local'

extension local

resource getKubeConfig 'Command' = {
  command: 'kubectl config view --raw'
}

module aksStoreApp 'kubernetes.bicep' = {
  params: {
    kubeConfig: base64(getKubeConfig.stdOut)
  }
}
