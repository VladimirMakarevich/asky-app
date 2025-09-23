targetScope = 'resourceGroup'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Azure region for the Key Vault.')
param location string

@description('Tags propagated to Key Vault resources.')
param tags object

@description('Speech resource identifier used to pull API keys.')
param speechAccountId string

@description('Speech service endpoint stored as secret metadata.')
param speechEndpoint string

@description('Primary SignalR connection string to be stored securely.')
@secure()
param signalRPrimaryConnectionString string

@description('Optional configuration overrides for Key Vault or secret naming.')
param keyVaultConfig object = {}

@description('Optional LLM settings that should be persisted as secrets.')
param llmConfig object = {}

@description('Principal IDs that should receive Secret User access.')
param principalIds array = []

@description('Subnet ID used for the Key Vault private endpoint.')
param privateEndpointSubnetId string

@description('Virtual network ID used for DNS links.')
param virtualNetworkId string

var keyVaultName = contains(keyVaultConfig, 'name') ? keyVaultConfig.name : toLower(format('{0}-{1}-kv', baseName, environmentName))
var keyVaultSku = contains(keyVaultConfig, 'sku') ? keyVaultConfig.sku : 'standard'
var tenantId = tenant().tenantId

var llmApiKey = contains(llmConfig, 'apiKey') ? string(llmConfig.apiKey) : ''
var llmEndpoint = contains(llmConfig, 'endpoint') ? string(llmConfig.endpoint) : ''
var llmDeployment = contains(llmConfig, 'deployment') ? string(llmConfig.deployment) : ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: tenantId
    sku: {
      family: 'A'
      name: toUpper(keyVaultSku)
    }
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Disabled'
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      ipRules: []
      virtualNetworkRules: []
    }
  }
}

var privateEndpointName = toLower(format('{0}-{1}-kv-pe', baseName, environmentName))
var privateDnsZoneName = 'privatelink.vaultcore.azure.net'
var dnsZoneLinkName = format('kv-{0}-link', environmentName)

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = {
  name: privateEndpointName
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: format('{0}-{1}-kv-pls', baseName, environmentName)
        properties: {
          groupIds: [
            'vault'
          ]
          privateLinkServiceId: keyVault.id
        }
      }
    ]
  }
}

resource keyVaultPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZoneName
  location: 'Global'
  tags: tags
}

resource keyVaultDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: format('{0}/{1}', keyVaultPrivateDnsZone.name, dnsZoneLinkName)
  location: 'Global'
  properties: {
    virtualNetwork: {
      id: virtualNetworkId
    }
    registrationEnabled: false
  }
}

resource keyVaultDnsZoneGroup 'Microsoft.Network/privateEndpoints/dnsZoneGroups@2020-05-01' = {
  name: format('{0}/kv-dns-zone-group', keyVaultPrivateEndpoint.name)
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'vault'
        properties: {
          privateDnsZoneId: keyVaultPrivateDnsZone.id
        }
      }
    ]
  }
}

var speechKeys = listKeys(speechAccountId, '2023-05-01')

resource speechKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: format('{0}/speech-key', keyVault.name)
  properties: {
    value: speechKeys.keys[0].value
    contentType: 'text/plain'
  }
  dependsOn: [
    keyVault
  ]
}

resource speechEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: format('{0}/speech-endpoint', keyVault.name)
  properties: {
    value: speechEndpoint
    contentType: 'text/plain'
  }
  dependsOn: [
    keyVault
  ]
}

resource signalRConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: format('{0}/signalr-connection', keyVault.name)
  properties: {
    value: signalRPrimaryConnectionString
    contentType: 'text/plain'
  }
  dependsOn: [
    keyVault
  ]
}

resource llmApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (llmApiKey != '') {
  name: format('{0}/llm-api-key', keyVault.name)
  properties: {
    value: llmApiKey
    contentType: 'text/plain'
  }
  dependsOn: [
    keyVault
  ]
}

resource llmEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (llmEndpoint != '') {
  name: format('{0}/llm-endpoint', keyVault.name)
  properties: {
    value: llmEndpoint
    contentType: 'text/plain'
  }
  dependsOn: [
    keyVault
  ]
}

resource llmDeploymentSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (llmDeployment != '') {
  name: format('{0}/llm-deployment', keyVault.name)
  properties: {
    value: llmDeployment
    contentType: 'text/plain'
  }
  dependsOn: [
    keyVault
  ]
}

resource keyVaultRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in principalIds: {
  name: guid(keyVault.id, principalId, 'kv-secret-user')
  scope: keyVault
  properties: {
    principalId: principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalType: 'ServicePrincipal'
  }
}]

output keyVaultId string = keyVault.id
output keyVaultUri string = keyVault.properties.vaultUri
output privateEndpointId string = keyVaultPrivateEndpoint.id
