targetScope = 'subscription'

@description('Base name prefix for all resources including resource groups and shared components.')
param baseName string = 'asky'

@description('Primary Azure region to deploy shared resources when an environment does not override it.')
param defaultLocation string = 'westeurope'

@description('Per-environment configuration including optional overrides for resource SKUs and locations.')
param environmentConfigs array = [
  {
    name: 'dev'
  }
  {
    name: 'stage'
  }
  {
    name: 'prod'
  }
]

@description('Tags applied to every resource deployed by this template.')
param baseTags object = {
  project: baseName
}

module resourceGroups 'resource-groups.bicep' = {
  name: 'resourceGroups'
  params: {
    baseName: baseName
    environmentConfigs: [for env in environmentConfigs: {
      name: env.name
      location: contains(env, 'location') ? env.location : defaultLocation
    }]
    baseTags: baseTags
  }
}

var environmentContexts = [for (env, index) in environmentConfigs: {
  name: env.name
  resourceGroupName: resourceGroups.outputs.resourceGroupNames[index]
  location: contains(env, 'location') ? env.location : defaultLocation
  tags: union(union(baseTags, { environment: env.name }), contains(env, 'tags') ? env.tags : {})
  config: env
}]

module networks 'modules/network.bicep' = [for env in environmentContexts: {
  name: format('network-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    baseName: baseName
    environmentName: env.name
    location: env.location
    tags: env.tags
    networkConfig: contains(env.config, 'network') ? env.config.network : {}
  }
}]

module monitoring 'modules/monitoring.bicep' = [for env in environmentContexts: {
  name: format('monitoring-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    baseName: baseName
    environmentName: env.name
    location: env.location
    tags: env.tags
    monitoringConfig: contains(env.config, 'monitoring') ? env.config.monitoring : {}
  }
}]

module signalr 'modules/signalr.bicep' = [for (env, index) in environmentContexts: {
  name: format('signalr-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    baseName: baseName
    environmentName: env.name
    location: env.location
    tags: env.tags
    privateEndpointSubnetId: networks[index].outputs.privateEndpointSubnetId
    virtualNetworkId: networks[index].outputs.vnetId
    signalRConfig: contains(env.config, 'signalR') ? env.config.signalR : {}
  }
}]

module appService 'modules/appservice.bicep' = [for (env, index) in environmentContexts: {
  name: format('appsvc-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    baseName: baseName
    environmentName: env.name
    location: env.location
    tags: env.tags
    appSubnetId: networks[index].outputs.appSubnetId
    appServiceConfig: union(contains(env.config, 'appService') ? env.config.appService : {}, {
      appSettings: concat(contains(env.config, 'appService') && contains(env.config.appService, 'appSettings') ? env.config.appService.appSettings : [], [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: monitoring[index].outputs.appInsightsConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_INSTRUMENTATIONKEY'
          value: monitoring[index].outputs.appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_ROLE_NAME'
          value: format('{0}-{1}', baseName, env.name)
        }
      ])
    })
  }
}]

module frontDoor 'modules/frontdoor.bicep' = [for (env, index) in environmentContexts: {
  name: format('frontdoor-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    baseName: baseName
    environmentName: env.name
    tags: env.tags
    originHostName: appService[index].outputs.defaultHostname
    frontDoorConfig: contains(env.config, 'frontDoor') ? env.config.frontDoor : {}
  }
}]

module speech 'modules/speech.bicep' = [for env in environmentContexts: {
  name: format('speech-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    baseName: baseName
    environmentName: env.name
    location: env.location
    tags: env.tags
    speechConfig: contains(env.config, 'speech') ? env.config.speech : {}
  }
}]

module keyVault 'modules/keyvault.bicep' = [for (env, index) in environmentContexts: {
  name: format('kv-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    baseName: baseName
    environmentName: env.name
    location: env.location
    tags: env.tags
    speechAccountId: speech[index].outputs.id
    speechEndpoint: speech[index].outputs.endpoint
    signalRPrimaryConnectionString: signalr[index].outputs.primaryConnectionString
    keyVaultConfig: contains(env.config, 'keyVault') ? env.config.keyVault : {}
    llmConfig: contains(env.config, 'llm') ? env.config.llm : {}
    principalIds: [appService[index].outputs.principalId]
    privateEndpointSubnetId: networks[index].outputs.privateEndpointSubnetId
    virtualNetworkId: networks[index].outputs.vnetId
  }
}]

module diagnostics 'modules/diagnostics.bicep' = [for (env, index) in environmentContexts: {
  name: format('diagnostics-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    workspaceId: monitoring[index].outputs.workspaceId
    appServiceName: appService[index].outputs.name
    signalRName: signalr[index].outputs.name
  }
}]

module alerts 'modules/alerts.bicep' = [for (env, index) in environmentContexts: {
  name: format('alerts-{0}', env.name)
  scope: resourceGroup(env.resourceGroupName)
  params: {
    baseName: baseName
    environmentName: env.name
    location: env.location
    tags: env.tags
    frontDoorProfileId: frontDoor[index].outputs.profileId
    workspaceId: monitoring[index].outputs.workspaceId
    emailReceivers: contains(env.config, 'alerts') && contains(env.config.alerts, 'emails') ? env.config.alerts.emails : []
    hubAvailabilityThreshold: contains(env.config, 'alerts') && contains(env.config.alerts, 'hubAvailabilityThreshold') ? env.config.alerts.hubAvailabilityThreshold : 95
    llmLatencyThresholdMs: contains(env.config, 'alerts') && contains(env.config.alerts, 'llmLatencyThresholdMs') ? env.config.alerts.llmLatencyThresholdMs : 4000
  }
}]

module speechBudget 'modules/speechBudget.bicep' = [for (env, index) in environmentContexts: {
  name: format('speech-budget-{0}', env.name)
  params: {
    baseName: baseName
    environmentName: env.name
    speechResourceId: speech[index].outputs.id
    actionGroupId: alerts[index].outputs.actionGroupId
    monthlyBudgetAmount: contains(env.config, 'alerts') && contains(env.config.alerts, 'speechMonthlyBudget') ? env.config.alerts.speechMonthlyBudget : 500
    additionalEmails: contains(env.config, 'alerts') && contains(env.config.alerts, 'emails') ? env.config.alerts.emails : []
  }
}]

output environments array = environmentContexts
output networks array = [for (env, index) in environmentContexts: {
  environment: env.name
  vnetId: networks[index].outputs.vnetId
  privateEndpointSubnetId: networks[index].outputs.privateEndpointSubnetId
  appSubnetId: networks[index].outputs.appSubnetId
}]
output monitoringResources array = [for (env, index) in environmentContexts: {
  environment: env.name
  workspaceId: monitoring[index].outputs.workspaceId
  appInsightsId: monitoring[index].outputs.appInsightsId
}]
output appServices array = [for (env, index) in environmentContexts: {
  environment: env.name
  id: appService[index].outputs.appServiceId
  name: appService[index].outputs.name
  defaultHostname: appService[index].outputs.defaultHostname
  principalId: appService[index].outputs.principalId
  planId: appService[index].outputs.planId
}]
output speechAccounts array = [for (env, index) in environmentContexts: {
  environment: env.name
  id: speech[index].outputs.id
  name: speech[index].outputs.name
  endpoint: speech[index].outputs.endpoint
  subdomain: speech[index].outputs.subdomain
}]
output signalrAccounts array = [for (env, index) in environmentContexts: {
  environment: env.name
  id: signalr[index].outputs.id
  name: signalr[index].outputs.name
  hostName: signalr[index].outputs.hostName
  privateEndpointId: signalr[index].outputs.privateEndpointId
  privateDnsZoneId: signalr[index].outputs.privateDnsZoneId
}]
output frontDoors array = [for (env, index) in environmentContexts: {
  environment: env.name
  host: frontDoor[index].outputs.frontDoorHost
  customDomain: frontDoor[index].outputs.customDomainHost
  profileId: frontDoor[index].outputs.profileId
}]
output keyVaults array = [for (env, index) in environmentContexts: {
  environment: env.name
  id: keyVault[index].outputs.keyVaultId
  uri: keyVault[index].outputs.keyVaultUri
  privateEndpointId: keyVault[index].outputs.privateEndpointId
}]
output alerting array = [for (env, index) in environmentContexts: {
  environment: env.name
  actionGroupId: alerts[index].outputs.actionGroupId
}]
