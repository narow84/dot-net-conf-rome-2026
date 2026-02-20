@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param userPrincipalId string = ''

param tags object = { }

param stage_acr_outputs_name string

resource stage_mi 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('stage_mi-${uniqueString(resourceGroup().id)}', 128)
  location: location
  tags: tags
}

resource stage_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: stage_acr_outputs_name
}

resource stage_acr_stage_mi_AcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(stage_acr.id, stage_mi.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d'))
  properties: {
    principalId: stage_mi.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalType: 'ServicePrincipal'
  }
  scope: stage_acr
}

resource stage_law 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: take('stagelaw-${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
  tags: tags
}

resource stage 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: take('stage${uniqueString(resourceGroup().id)}', 24)
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: stage_law.properties.customerId
        sharedKey: stage_law.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
  tags: tags
}

resource aspireDashboard 'Microsoft.App/managedEnvironments/dotNetComponents@2024-10-02-preview' = {
  name: 'aspire-dashboard'
  properties: {
    componentType: 'AspireDashboard'
  }
  parent: stage
}

resource stage_storageVolume 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: take('stagestoragevolume${uniqueString(resourceGroup().id)}', 24)
  kind: 'StorageV2'
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    largeFileSharesState: 'Enabled'
    minimumTlsVersion: 'TLS1_2'
  }
  tags: tags
}

resource storageVolumeFileService 'Microsoft.Storage/storageAccounts/fileServices@2024-01-01' = {
  name: 'default'
  parent: stage_storageVolume
}

resource shares_volumes_postgres_0 'Microsoft.Storage/storageAccounts/fileServices/shares@2024-01-01' = {
  name: take('sharesvolumespostgres0-${uniqueString(resourceGroup().id)}', 63)
  properties: {
    enabledProtocols: 'SMB'
    shareQuota: 1024
  }
  parent: storageVolumeFileService
}

resource managedStorage_volumes_postgres_0 'Microsoft.App/managedEnvironments/storages@2025-01-01' = {
  name: take('managedstoragevolumespostgres${uniqueString(resourceGroup().id)}', 24)
  properties: {
    azureFile: {
      accountName: stage_storageVolume.name
      accountKey: stage_storageVolume.listKeys().keys[0].value
      accessMode: 'ReadWrite'
      shareName: shares_volumes_postgres_0.name
    }
  }
  parent: stage
}

resource shares_bindmounts_postgres_0 'Microsoft.Storage/storageAccounts/fileServices/shares@2024-01-01' = {
  name: take('sharesbindmountspostgres0-${uniqueString(resourceGroup().id)}', 63)
  properties: {
    enabledProtocols: 'SMB'
    shareQuota: 1024
  }
  parent: storageVolumeFileService
}

resource managedStorage_bindmounts_postgres_0 'Microsoft.App/managedEnvironments/storages@2025-01-01' = {
  name: take('managedstoragebindmountspostgres${uniqueString(resourceGroup().id)}', 24)
  properties: {
    azureFile: {
      accountName: stage_storageVolume.name
      accountKey: stage_storageVolume.listKeys().keys[0].value
      accessMode: 'ReadWrite'
      shareName: shares_bindmounts_postgres_0.name
    }
  }
  parent: stage
}

output volumes_postgres_0 string = managedStorage_volumes_postgres_0.name

output bindmounts_postgres_0 string = managedStorage_bindmounts_postgres_0.name

output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = stage_law.name

output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = stage_law.id

output AZURE_CONTAINER_REGISTRY_NAME string = stage_acr.name

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = stage_acr.properties.loginServer

output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = stage_mi.id

output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = stage.name

output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = stage.id

output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = stage.properties.defaultDomain