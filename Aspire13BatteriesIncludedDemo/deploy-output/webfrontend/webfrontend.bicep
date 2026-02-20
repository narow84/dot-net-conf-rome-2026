@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param stage_outputs_azure_container_apps_environment_default_domain string

param stage_outputs_azure_container_apps_environment_id string

param webfrontend_containerimage string

param webfrontend_containerport string

param stage_outputs_azure_container_registry_endpoint string

param stage_outputs_azure_container_registry_managed_identity_id string

resource webfrontend 'Microsoft.App/containerApps@2025-02-02-preview' = {
  name: 'webfrontend'
  location: location
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: int(webfrontend_containerport)
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
          image: webfrontend_containerimage
          name: 'webfrontend'
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
              value: webfrontend_containerport
            }
            {
              name: 'APISERVICE_HTTP'
              value: 'http://apiservice.internal.${stage_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__apiservice__http__0'
              value: 'http://apiservice.internal.${stage_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'APISERVICE_HTTPS'
              value: 'https://apiservice.internal.${stage_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__apiservice__https__0'
              value: 'https://apiservice.internal.${stage_outputs_azure_container_apps_environment_default_domain}'
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
      '${stage_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}