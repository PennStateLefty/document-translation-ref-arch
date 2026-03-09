@description('Name prefix for the managed identity')
param namePrefix string

@description('Azure region')
param location string = resourceGroup().location

@description('Tags to apply to all resources')
param tags object = {}

@description('GitHub repository owner (org or user)')
param repositoryOwner string

@description('GitHub repository name')
param repositoryName string

resource userManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${namePrefix}-id'
  location: location
  tags: tags
}

// Federated credential for pushes to main (deploy workflow)
resource federatedCredentialMain 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: userManagedIdentity
  name: 'github-deploy-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${repositoryOwner}/${repositoryName}:ref:refs/heads/main'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

// Federated credential for pull requests (bicep-validate workflow)
resource federatedCredentialPR 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: userManagedIdentity
  name: 'github-validate-pr'
  dependsOn: [federatedCredentialMain]
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${repositoryOwner}/${repositoryName}:pull_request'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

output umiId string = userManagedIdentity.id
output umiPrincipalId string = userManagedIdentity.properties.principalId
output umiClientId string = userManagedIdentity.properties.clientId
output umiName string = userManagedIdentity.name
