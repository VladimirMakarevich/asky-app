targetScope = 'resourceGroup'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Azure region for monitoring resources.')
param location string

@description('Tags propagated to monitoring resources.')
param tags object

@description('Optional configuration overrides for Log Analytics and App Insights.')
param monitoringConfig object = {}

var workspaceName = contains(monitoringConfig, 'workspaceName') ? monitoringConfig.workspaceName : toLower(format('{0}-{1}-log', baseName, environmentName))
var workspaceSku = contains(monitoringConfig, 'workspaceSku') ? monitoringConfig.workspaceSku : 'PerGB2018'
var workspaceRetention = contains(monitoringConfig, 'workspaceRetentionInDays') ? monitoringConfig.workspaceRetentionInDays : 30

var appInsightsName = contains(monitoringConfig, 'appInsightsName') ? monitoringConfig.appInsightsName : toLower(format('{0}-{1}-appi', baseName, environmentName))
var appInsightsType = contains(monitoringConfig, 'applicationType') ? monitoringConfig.applicationType : 'web'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    retentionInDays: workspaceRetention
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
  sku: {
    name: workspaceSku
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: appInsightsType
    Flow_Type: 'Redfield'
    WorkspaceResourceId: logAnalytics.id
    DisableIpMasking: true
    IngestionMode: 'LogAnalytics'
  }
}

output workspaceId string = logAnalytics.id
output workspaceName string = logAnalytics.name
output appInsightsId string = appInsights.id
output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
