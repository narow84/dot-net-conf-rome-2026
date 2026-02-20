@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param stage_outputs_azure_container_apps_environment_default_domain string

param stage_outputs_azure_container_apps_environment_id string

param apiservice_containerimage string

param apiservice_identity_outputs_id string

param apiservice_containerport string

@secure()
param pg_password_value string

param ai_foundry_outputs_endpoint string

param ai_foundry_outputs_aifoundryapiendpoint string

param apiservice_identity_outputs_clientid string

param stage_outputs_azure_container_registry_endpoint string

param stage_outputs_azure_container_registry_managed_identity_id string

resource apiservice 'Microsoft.App/containerApps@2025-02-02-preview' = {
  name: 'apiservice'
  location: location
  properties: {
    configuration: {
      secrets: [
        {
          name: 'connectionstrings--appdb'
          value: 'Host=postgres;Port=5432;Username=postgres;Password=${pg_password_value};Database=catalog-db'
        }
        {
          name: 'appdb-password'
          value: pg_password_value
        }
        {
          name: 'appdb-uri'
          value: 'postgresql://postgres:${uriComponent(pg_password_value)}@postgres:5432/catalog-db'
        }
      ]
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: int(apiservice_containerport)
        transport: 'http'
      }
      registries: [
        {
          server: stage_outputs_azure_container_registry_endpoint
          identity: stage_outputs_azure_container_registry_managed_identity_id
        }
      ]
      runtime: {
        dotnet: {
          autoConfigureDataProtection: true
        }
      }
    }
    environmentId: stage_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: apiservice_containerimage
          name: 'apiservice'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES'
              value: 'true'
            }
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES'
              value: 'true'
            }
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'HTTP_PORTS'
              value: apiservice_containerport
            }
            {
              name: 'ConnectionStrings__appDb'
              secretRef: 'connectionstrings--appdb'
            }
            {
              name: 'APPDB_HOST'
              value: 'postgres'
            }
            {
              name: 'APPDB_PORT'
              value: '5432'
            }
            {
              name: 'APPDB_USERNAME'
              value: 'postgres'
            }
            {
              name: 'APPDB_PASSWORD'
              secretRef: 'appdb-password'
            }
            {
              name: 'APPDB_URI'
              secretRef: 'appdb-uri'
            }
            {
              name: 'APPDB_JDBCCONNECTIONSTRING'
              value: 'jdbc:postgresql://postgres:5432/catalog-db'
            }
            {
              name: 'APPDB_DATABASENAME'
              value: 'catalog-db'
            }
            {
              name: 'ConnectionStrings__chat'
              value: 'Endpoint=${ai_foundry_outputs_endpoint};EndpointAIInference=${ai_foundry_outputs_aifoundryapiendpoint}models;Deployment=chat'
            }
            {
              name: 'CHAT_URI'
              value: ai_foundry_outputs_aifoundryapiendpoint
            }
            {
              name: 'CHAT_MODELNAME'
              value: 'chat'
            }
            {
              name: 'CHAT_FORMAT'
              value: 'OpenAI'
            }
            {
              name: 'CHAT_VERSION'
              value: '2024-07-18'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: apiservice_identity_outputs_clientid
            }
            {
              name: 'AZURE_TOKEN_CREDENTIALS'
              value: 'ManagedIdentityCredential'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiservice_identity_outputs_id}': { }
      '${stage_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}