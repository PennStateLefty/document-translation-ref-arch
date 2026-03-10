@description('Name prefix for the Static Web App')
param namePrefix string

@description('Azure region')
param location string = resourceGroup().location

@description('Tags to apply to all resources')
param tags object = {}

@description('Function App resource ID to link as backend')
param functionAppResourceId string = ''

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: '${namePrefix}-swa'
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    stagingEnvironmentPolicy: 'Enabled'
    allowConfigFileUpdates: true
  }
}

resource linkedBackend 'Microsoft.Web/staticSites/linkedBackends@2023-01-01' = if (!empty(functionAppResourceId)) {
  parent: staticWebApp
  name: 'backend'
  properties: {
    backendResourceId: functionAppResourceId
    region: location
  }
}

output staticWebAppId string = staticWebApp.id
output staticWebAppName string = staticWebApp.name
output staticWebAppDefaultHostName string = staticWebApp.properties.defaultHostname
