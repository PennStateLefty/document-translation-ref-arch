@description('Name prefix for the Translator resource')
param namePrefix string

@description('Azure region — must be a specific geographic region (not Global) for Document Translation managed identity support')
param location string = resourceGroup().location

@description('Tags to apply to all resources')
param tags object = {}

resource translator 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: '${namePrefix}-translator'
  location: location
  tags: tags
  kind: 'TextTranslation'
  sku: {
    name: 'S1'
  }
  properties: {
    customSubDomainName: '${namePrefix}-translator'
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
  }
  identity: {
    type: 'SystemAssigned'
  }
}

output translatorId string = translator.id
output translatorEndpoint string = translator.properties.endpoint
output translatorName string = translator.name
output translatorPrincipalId string = translator.identity.principalId
