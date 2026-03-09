@description('Principal ID of the Function App managed identity')
param functionAppPrincipalId string

@description('Storage Account name')
param storageAccountName string

@description('AI Services account name')
param aiServicesName string

@description('Principal ID of the AI Services system-assigned managed identity')
param aiServicesPrincipalId string

// Role definition IDs
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var storageAccountContributorRoleId = '17d1049b-9a84-46fb-8f53-869881c3d3ab'
var cognitiveServicesUserRoleId = 'a97b65f3-24c7-4388-baec-2e87135dc908'
var monitoringMetricsPublisherRoleId = '3913510d-42f4-4e42-8a64-420c390055eb'

// Existing resource references for scoping
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource aiServices 'Microsoft.CognitiveServices/accounts@2024-10-01' existing = {
  name: aiServicesName
}

// Function App: Storage Blob Data Contributor
resource storageBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Function App: Cognitive Services User
resource cognitiveServicesUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiServices.id, functionAppPrincipalId, cognitiveServicesUserRoleId)
  scope: aiServices
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesUserRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Function App: Storage Blob Data Owner (identity-based AzureWebJobsStorage)
resource storageBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageBlobDataOwnerRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Function App: Storage Queue Data Contributor (Functions internal queues)
resource storageQueueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageQueueDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Function App: Storage Table Data Contributor (Durable Functions state)
resource storageTableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Function App: Storage Account Contributor (content share management on Consumption plan)
resource storageAccountContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppPrincipalId, storageAccountContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageAccountContributorRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// AI Services MI: Storage Blob Data Contributor (Document Translation reads source / writes translated blobs)
resource aiServicesStorageBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, aiServicesPrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: aiServicesPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Function App: Monitoring Metrics Publisher (AAD-authenticated App Insights telemetry ingestion)
resource monitoringMetricsPublisher 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, functionAppPrincipalId, monitoringMetricsPublisherRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisherRoleId)
    principalId: functionAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}
