targetScope = 'resourceGroup'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Azure region for the SignalR resource.')
param location string

@description('Tags propagated to the SignalR resources.')
param tags object

@description('Subnet ID that hosts private endpoints.')
param privateEndpointSubnetId string

@description('Virtual network resource ID used for DNS links.')
param virtualNetworkId string

@description('Optional configuration overrides for SignalR.')
param signalRConfig object = {}

var signalRName = contains(signalRConfig, 'name') ? signalRConfig.name : toLower(format('{0}-{1}-signalr', baseName, environmentName))
var signalRLocation = contains(signalRConfig, 'location') ? signalRConfig.location : location
var signalRSkuName = contains(signalRConfig, 'skuName') ? signalRConfig.skuName : 'Standard_S1'
var signalRCapacity = contains(signalRConfig, 'capacity') ? signalRConfig.capacity : 1
var signalRPublicNetworkAccess = contains(signalRConfig, 'publicNetworkAccess') ? signalRConfig.publicNetworkAccess : 'Disabled'
var signalRServiceMode = contains(signalRConfig, 'serviceMode') ? signalRConfig.serviceMode : 'Serverless'
var signalREnableConnectivityLogs = contains(signalRConfig, 'enableConnectivityLogs') ? signalRConfig.enableConnectivityLogs : true
var signalREnableMessagingLogs = contains(signalRConfig, 'enableMessagingLogs') ? signalRConfig.enableMessagingLogs : true
var signalRDisableLocalAuth = contains(signalRConfig, 'disableLocalAuth') ? signalRConfig.disableLocalAuth : true

resource signalR 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: signalRName
  location: signalRLocation
  tags: tags
  sku: {
    name: signalRSkuName
    capacity: signalRCapacity
  }
  kind: 'SignalR'
  properties: {
    cors: {
      allowedOrigins: []
    }
    disableLocalAuth: signalRDisableLocalAuth
    publicNetworkAccess: signalRPublicNetworkAccess
    features: [
      {
        flag: 'ServiceMode'
        value: signalRServiceMode
      }
      {
        flag: 'EnableConnectivityLogs'
        value: signalREnableConnectivityLogs ? 'True' : 'False'
      }
      {
        flag: 'EnableMessagingLogs'
        value: signalREnableMessagingLogs ? 'True' : 'False'
      }
      {
        flag: 'DisableLocalAuth'
        value: signalRDisableLocalAuth ? 'True' : 'False'
      }
    ]
  }
}

var privateEndpointName = toLower(format('{0}-{1}-signalr-pe', baseName, environmentName))
var privateDnsZoneName = 'privatelink.service.signalr.net'
var dnsZoneVirtualNetworkLinkName = format('signalr-{0}-link', environmentName)

resource signalrPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = {
  name: privateEndpointName
  location: signalRLocation
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: format('{0}-{1}-signalr-pls', baseName, environmentName)
        properties: {
          groupIds: [
            'signalr'
          ]
          privateLinkServiceId: signalR.id
        }
      }
    ]
  }
}

resource signalrPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: privateDnsZoneName
  location: 'Global'
  tags: tags
}

resource signalrDnsVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  name: format('{0}/{1}', signalrPrivateDnsZone.name, dnsZoneVirtualNetworkLinkName)
  location: 'Global'
  properties: {
    virtualNetwork: {
      id: virtualNetworkId
    }
    registrationEnabled: false
  }
}

resource signalrDnsZoneGroup 'Microsoft.Network/privateEndpoints/dnsZoneGroups@2020-05-01' = {
  name: format('{0}/signalr-dns-zone-group', signalrPrivateEndpoint.name)
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'signalr'
        properties: {
          privateDnsZoneId: signalrPrivateDnsZone.id
        }
      }
    ]
  }
}

var signalRKeys = listKeys(signalR.id, '2023-02-01')

output id string = signalR.id
output name string = signalR.name
output hostName string = signalR.properties.hostName
output primaryConnectionString string = signalRKeys.primaryConnectionString
output secondaryConnectionString string = signalRKeys.secondaryConnectionString
output privateEndpointId string = signalrPrivateEndpoint.id
output privateDnsZoneId string = signalrPrivateDnsZone.id
