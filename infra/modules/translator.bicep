@description('Name prefix for the translator resource')
param namePrefix string

@description('Azure region')
param location string = resourceGroup().location

@description('Tags to apply to all resources')
param tags object = {}

@description('SKU for the Translator resource')
param skuName string = 'S1'

resource translator 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: '${namePrefix}-translator'
  location: location
  tags: tags
  kind: 'TextTranslation'
  sku: {
    name: skuName
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
