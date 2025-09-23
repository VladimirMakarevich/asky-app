targetScope = 'subscription'

@description('Base name prefix for all resource groups.')
param baseName string = 'asky'

@description('Environment definitions with individual deployment regions.')
param environmentConfigs array = [
  {
    name: 'dev'
    location: 'westeurope'
  }
  {
    name: 'stage'
    location: 'westeurope'
  }
  {
    name: 'prod'
    location: 'westeurope'
  }
]

@description('Common tags that will be applied to every resource group.')
param baseTags object = {
  project: baseName
}

resource environmentResourceGroups 'Microsoft.Resources/resourceGroups@2022-09-01' = [for env in environmentConfigs: {
  name: '${baseName}-${env.name}'
  location: env.location
  tags: union(baseTags, {
    environment: env.name
  })
}]

output resourceGroupNames array = [for rg in environmentResourceGroups: rg.name]
