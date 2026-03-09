@description('Name prefix for the Function App')
param namePrefix string

@description('Azure region')
param location string = resourceGroup().location

@description('Tags to apply to all resources')
param tags object = {}

@description('Storage account name for identity-based access')
param storageAccountName string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Translator endpoint URL')
param translatorEndpoint string

resource hostingPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${namePrefix}-plan'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${namePrefix}-func'
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: 'https://${storageAccountName}.blob.${environment().suffixes.storage}/deployments'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 100
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccountName
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'
          value: 'Authorization=AAD'
        }
        {
          name: 'TRANSLATOR_ENDPOINT'
          value: translatorEndpoint
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storageAccountName
        }
      ]
    }
  }
}

// Disable SCM basic auth (Secure Future Initiative)
resource scmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: functionApp
  name: 'scm'
  properties: {
    allow: false
  }
}

// Disable FTP basic auth
resource ftpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = {
  parent: functionApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

output functionAppId string = functionApp.id
output functionAppName string = functionApp.name
output functionAppPrincipalId string = functionApp.identity.principalId
output functionAppHostName string = functionApp.properties.defaultHostName
