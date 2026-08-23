#!/usr/bin/env pwsh
<#
.SYNOPSIS
Refreshes the vendored Bicep type definitions under ./types.

.DESCRIPTION
The type definitions are generated and published by the Azure/bicep-types-k8s repository. This script copies
them into ./types verbatim, so that they can be embedded into the extension binary and served at runtime by
EmbeddedTypeDefinitionBuilder.

.PARAMETER KubernetesVersion
The Kubernetes version to vendor, e.g. 'v1.33.0'. Must correspond to a directory under 'generated' in the
Azure/bicep-types-k8s repository.
#>
[cmdletbinding()]
param(
   [Parameter(Mandatory=$false)][string]$KubernetesVersion = "v1.33.0",
   [Parameter(Mandatory=$false)][string]$Repository = "https://github.com/Azure/bicep-types-k8s.git",
   [Parameter(Mandatory=$false)][string]$Ref = "main"
)

$ErrorActionPreference = "Stop"

function ExecSafe([scriptblock] $ScriptBlock) {
  & $ScriptBlock
  if ($LASTEXITCODE -ne 0) {
      exit $LASTEXITCODE
  }
}

$root = "$PSScriptRoot/.."
$destination = Join-Path $root "types"
$checkout = Join-Path ([System.IO.Path]::GetTempPath()) "bicep-types-k8s-$([guid]::NewGuid().ToString('n'))"

try {
  # A blobless sparse clone keeps this to a few MB rather than the full repository history.
  ExecSafe { git clone --depth 1 --branch $Ref --filter=blob:none --sparse $Repository $checkout }
  ExecSafe { git -C $checkout sparse-checkout set "generated/$KubernetesVersion" }

  $source = Join-Path $checkout "generated/$KubernetesVersion"
  if (-not (Test-Path $source)) {
    throw "'$KubernetesVersion' was not found in $Repository. Check the 'generated' directory for available versions."
  }

  if (Test-Path $destination) {
    Remove-Item -Recurse -Force $destination
  }

  New-Item -ItemType Directory -Path $destination | Out-Null
  Copy-Item -Recurse -Force -Path (Join-Path $source "*") -Destination $destination

  $commit = & git -C $checkout rev-parse HEAD
  Write-Host "Vendored $KubernetesVersion types from $Repository@$commit into $destination."
}
finally {
  if (Test-Path $checkout) {
    Remove-Item -Recurse -Force $checkout
  }
}
