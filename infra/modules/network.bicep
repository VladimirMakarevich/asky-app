targetScope = 'resourceGroup'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Azure region for the network resources.')
param location string

@description('Tags propagated to networking resources.')
param tags object

@description('Optional configuration overrides for the virtual network and subnets.')
param networkConfig object = {}

var defaultAddressSpaceMap = {
  dev: '10.10.0.0/16'
  stage: '10.20.0.0/16'
  prod: '10.30.0.0/16'
}

var defaultPeSubnetMap = {
  dev: '10.10.1.0/24'
  stage: '10.20.1.0/24'
  prod: '10.30.1.0/24'
}

var defaultAppSubnetMap = {
  dev: '10.10.2.0/24'
  stage: '10.20.2.0/24'
  prod: '10.30.2.0/24'
}

var vnetName = contains(networkConfig, 'name') ? networkConfig.name : toLower(format('{0}-{1}-vnet', baseName, environmentName))
var vnetLocation = contains(networkConfig, 'location') ? networkConfig.location : location
var vnetAddressSpace = contains(networkConfig, 'addressSpace') ? networkConfig.addressSpace : (contains(defaultAddressSpaceMap, environmentName) ? defaultAddressSpaceMap[environmentName] : '10.0.0.0/16')

var privateEndpointSubnet = {
  name: contains(networkConfig, 'privateEndpointSubnet') && contains(networkConfig.privateEndpointSubnet, 'name') ? networkConfig.privateEndpointSubnet.name : 'private-endpoints'
  prefix: contains(networkConfig, 'privateEndpointSubnet') && contains(networkConfig.privateEndpointSubnet, 'addressPrefix') ? networkConfig.privateEndpointSubnet.addressPrefix : (contains(defaultPeSubnetMap, environmentName) ? defaultPeSubnetMap[environmentName] : '10.0.1.0/24')
}

var appSubnet = {
  name: contains(networkConfig, 'appSubnet') && contains(networkConfig.appSubnet, 'name') ? networkConfig.appSubnet.name : 'app'
  prefix: contains(networkConfig, 'appSubnet') && contains(networkConfig.appSubnet, 'addressPrefix') ? networkConfig.appSubnet.addressPrefix : (contains(defaultAppSubnetMap, environmentName) ? defaultAppSubnetMap[environmentName] : '10.0.2.0/24')
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-05-01' = {
  name: vnetName
  location: vnetLocation
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressSpace
      ]
    }
    subnets: [
      {
        name: privateEndpointSubnet.name
        properties: {
          addressPrefix: privateEndpointSubnet.prefix
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Disabled'
        }
      }
      {
        name: appSubnet.name
        properties: {
          addressPrefix: appSubnet.prefix
          delegations: [
            {
              name: 'appservice-delegation'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
    ]
  }
}

output vnetId string = virtualNetwork.id
output vnetName string = virtualNetwork.name
output privateEndpointSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnetName, privateEndpointSubnet.name)
output appSubnetId string = resourceId('Microsoft.Network/virtualNetworks/subnets', vnetName, appSubnet.name)
