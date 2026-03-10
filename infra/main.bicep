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

@description('Principal ID (object ID) of the deployer identity (e.g. GitHub Actions UMI) for storage data-plane access during deployment')
param deployerPrincipalId string = ''

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
    aiServicesEndpoint: aiServices.outputs.translatorEndpoint
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
  }
}

module roleAssignments 'modules/role-assignments.bicep' = {
  name: 'role-assignments'
  scope: resourceGroup
  params: {
    functionAppPrincipalId: functionApp.outputs.functionAppPrincipalId
    storageAccountName: storage.outputs.storageAccountName
    translatorName: aiServices.outputs.translatorName
    translatorPrincipalId: aiServices.outputs.translatorPrincipalId
    deployerPrincipalId: deployerPrincipalId
  }
}

output functionAppName string = functionApp.outputs.functionAppName
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
output storageAccountName string = storage.outputs.storageAccountName
output functionAppUrl string = 'https://${functionApp.outputs.functionAppHostName}'
output staticWebAppUrl string = 'https://${staticWebApp.outputs.staticWebAppDefaultHostName}'
