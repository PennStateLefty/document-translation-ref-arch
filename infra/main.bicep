targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name prefix for all resources')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

@description('Tags to apply to all resources')
param tags object = {}

@description('GitHub repository owner (org or user)')
param repositoryOwner string

@description('GitHub repository name')
param repositoryName string

var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var namePrefix = '${environmentName}-${resourceToken}'

resource resourceGroup 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: union(tags, { 'azd-env-name': environmentName })
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  scope: resourceGroup
  params: {
    namePrefix: namePrefix
    location: location
    tags: tags
  }
}

module aiServices 'modules/ai-services.bicep' = {
  name: 'ai-services'
  scope: resourceGroup
  params: {
    namePrefix: namePrefix
    location: location
    tags: tags
  }
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: resourceGroup
  params: {
    namePrefix: namePrefix
    location: location
    tags: tags
  }
}

module functionApp 'modules/function-app.bicep' = {
  name: 'function-app'
  scope: resourceGroup
  params: {
    namePrefix: namePrefix
    location: location
    tags: tags
    storageAccountName: storage.outputs.storageAccountName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    aiServicesEndpoint: aiServices.outputs.aiServicesEndpoint
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'static-web-app'
  scope: resourceGroup
  params: {
    namePrefix: namePrefix
    location: location
    tags: tags
    functionAppResourceId: functionApp.outputs.functionAppId
    repositoryUrl: 'https://github.com/${repositoryOwner}/${repositoryName}'
  }
}

module roleAssignments 'modules/role-assignments.bicep' = {
  name: 'role-assignments'
  scope: resourceGroup
  params: {
    functionAppPrincipalId: functionApp.outputs.functionAppPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    aiServicesName: aiServices.outputs.aiServicesName
    aiServicesPrincipalId: aiServices.outputs.aiServicesPrincipalId
  }
}

output AZURE_FUNCTION_APP_NAME string = functionApp.outputs.functionAppName
output AZURE_STATIC_WEB_APP_NAME string = staticWebApp.outputs.staticWebAppName
output AZURE_STORAGE_ACCOUNT_NAME string = storage.outputs.storageAccountName
output AZURE_FUNCTION_APP_URL string = 'https://${functionApp.outputs.functionAppHostName}'
output AZURE_STATIC_WEB_APP_URL string = 'https://${staticWebApp.outputs.staticWebAppDefaultHostName}'
