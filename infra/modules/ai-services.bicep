@description('Name prefix for the AI Services resource')
param namePrefix string

@description('Azure region')
param location string = resourceGroup().location

@description('Tags to apply to all resources')
param tags object = {}

resource aiServices 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: '${namePrefix}-aiservices'
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: '${namePrefix}-aiservices'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    allowProjectManagement: true
  }
  identity: {
    type: 'SystemAssigned'
  }
}

output aiServicesId string = aiServices.id
output aiServicesEndpoint string = aiServices.properties.endpoint
output aiServicesName string = aiServices.name
output aiServicesPrincipalId string = aiServices.identity.principalId
