targetScope = 'subscription'

param resourceGroupName string

param location string

param principalId string

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

module stage_acr 'stage-acr/stage-acr.bicep' = {
  name: 'stage-acr'
  scope: rg
  params: {
    location: location
  }
}

module stage 'stage/stage.bicep' = {
  name: 'stage'
  scope: rg
  params: {
    location: location
    stage_acr_outputs_name: stage_acr.outputs.name
    userPrincipalId: principalId
  }
}

module ai_foundry 'ai-foundry/ai-foundry.bicep' = {
  name: 'ai-foundry'
  scope: rg
  params: {
    location: location
  }
}

module apiservice_identity 'apiservice-identity/apiservice-identity.bicep' = {
  name: 'apiservice-identity'
  scope: rg
  params: {
    location: location
  }
}

module apiservice_roles_ai_foundry 'apiservice-roles-ai-foundry/apiservice-roles-ai-foundry.bicep' = {
  name: 'apiservice-roles-ai-foundry'
  scope: rg
  params: {
    location: location
    ai_foundry_outputs_name: ai_foundry.outputs.name
    principalId: apiservice_identity.outputs.principalId
  }
}

output stage_acr_name string = stage_acr.outputs.name

output stage_acr_loginServer string = stage_acr.outputs.loginServer

output stage_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = stage.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID

output stage_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = stage.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN

output stage_AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = stage.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID

output stage_volumes_postgres_0 string = stage.outputs.volumes_postgres_0

output stage_bindmounts_postgres_0 string = stage.outputs.bindmounts_postgres_0

output stage_AZURE_CONTAINER_REGISTRY_ENDPOINT string = stage.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT

output apiservice_identity_id string = apiservice_identity.outputs.id

output ai_foundry_endpoint string = ai_foundry.outputs.endpoint

output ai_foundry_aiFoundryApiEndpoint string = ai_foundry.outputs.aiFoundryApiEndpoint

output apiservice_identity_clientId string = apiservice_identity.outputs.clientId