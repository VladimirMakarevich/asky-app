targetScope = 'resourceGroup'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Azure region for the Speech resource.')
param location string

@description('Tags propagated to the Speech resource for governance.')
param tags object

@description('Optional configuration overrides for the Speech resource.')
param speechConfig object = {}

var speechAccountName = toLower(format('{0}-{1}-speech', baseName, environmentName))
var defaultSubdomain = toLower(replace(format('{0}{1}', baseName, environmentName), '-', ''))
var speechSubdomain = contains(speechConfig, 'customSubdomainName') ? speechConfig.customSubdomainName : defaultSubdomain
var speechSku = contains(speechConfig, 'sku') ? speechConfig.sku : 'S0'
var speechTier = contains(speechConfig, 'skuTier') ? speechConfig.skuTier : 'Standard'
var speechPublicNetworkAccess = contains(speechConfig, 'publicNetworkAccess') ? speechConfig.publicNetworkAccess : 'Enabled'
var speechDisableLocalAuth = contains(speechConfig, 'disableLocalAuth') ? speechConfig.disableLocalAuth : true
var speechRestrictOutbound = contains(speechConfig, 'restrictOutboundNetworkAccess') ? speechConfig.restrictOutboundNetworkAccess : false
var speechNetworkAcls = contains(speechConfig, 'networkAcls') ? speechConfig.networkAcls : {
  defaultAction: 'Allow'
  ipRules: []
  virtualNetworkRules: []
  bypass: 'AzureServices'
}

resource speechAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: speechAccountName
  kind: 'SpeechServices'
  location: contains(speechConfig, 'location') ? speechConfig.location : location
  sku: {
    name: speechSku
    tier: speechTier
  }
  tags: tags
  properties: {
    customSubDomainName: speechSubdomain
    publicNetworkAccess: speechPublicNetworkAccess
    networkAcls: speechNetworkAcls
    restrictOutboundNetworkAccess: speechRestrictOutbound
    disableLocalAuth: speechDisableLocalAuth
  }
}

output id string = speechAccount.id
output name string = speechAccount.name
output endpoint string = speechAccount.properties.endpoint
output subdomain string = speechSubdomain
