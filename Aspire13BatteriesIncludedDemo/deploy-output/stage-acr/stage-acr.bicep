@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

resource stage_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: take('stageacr${uniqueString(resourceGroup().id)}', 50)
  location: location
  sku: {
    name: 'Basic'
  }
  tags: {
    'aspire-resource-name': 'stage-acr'
  }
}

output name string = stage_acr.name

output loginServer string = stage_acr.properties.loginServer