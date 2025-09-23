targetScope = 'resourceGroup'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Azure region for the App Service resources.')
param location string

@description('Tags propagated to App Service resources.')
param tags object

@description('Subnet ID used for regional VNet integration.')
param appSubnetId string

@description('Optional configuration overrides for App Service plan and site.')
param appServiceConfig object = {}

var planName = contains(appServiceConfig, 'planName') ? appServiceConfig.planName : toLower(format('{0}-{1}-asp', baseName, environmentName))
var planSkuName = contains(appServiceConfig, 'planSkuName') ? appServiceConfig.planSkuName : 'P1v3'
var planSkuTier = contains(appServiceConfig, 'planSkuTier') ? appServiceConfig.planSkuTier : 'PremiumV3'
var planCapacity = contains(appServiceConfig, 'planCapacity') ? appServiceConfig.planCapacity : 1

var siteName = contains(appServiceConfig, 'siteName') ? appServiceConfig.siteName : toLower(format('{0}-{1}-api', baseName, environmentName))
var siteLinuxFxVersion = contains(appServiceConfig, 'linuxFxVersion') ? appServiceConfig.linuxFxVersion : 'DOTNETCORE|9.0'
var siteAlwaysOn = contains(appServiceConfig, 'alwaysOn') ? appServiceConfig.alwaysOn : true
var siteAppSettings = contains(appServiceConfig, 'appSettings') ? appServiceConfig.appSettings : []
var baseAppSettings = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: toUpper(environmentName)
  }
  {
    name: 'WEBSITE_RUN_FROM_PACKAGE'
    value: '1'
  }
]

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: planSkuName
    tier: planSkuTier
    capacity: planCapacity
  }
  properties: {
    reserved: true
    perSiteScaling: false
    maximumElasticWorkerCount: 1
  }
}

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: siteName
  location: location
  kind: 'app,linux'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: siteLinuxFxVersion
      minimumTlsVersion: '1.2'
      http20Enabled: true
      ftpsState: 'Disabled'
      alwaysOn: siteAlwaysOn
      appSettings: concat(baseAppSettings, siteAppSettings)
    }
    virtualNetworkSubnetId: appSubnetId
  }
}

output appServiceId string = appService.id
output defaultHostname string = appService.properties.defaultHostName
output principalId string = appService.identity.principalId
output planId string = appServicePlan.id
output name string = appService.name
