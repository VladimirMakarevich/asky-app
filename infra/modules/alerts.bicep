targetScope = 'resourceGroup'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Azure region for regional alert resources (workspace location).')
param location string

@description('Tags propagated to alerting resources.')
param tags object

@description('Full resource ID of the Front Door profile to monitor for hub availability.')
param frontDoorProfileId string

@description('Full resource ID of the Log Analytics workspace for queries.')
param workspaceId string

@description('Optional list of email recipients for alerting.')
param emailReceivers array = []

@description('Threshold for Front Door origin health percentage.')
param hubAvailabilityThreshold int = 95

@description('LLM latency threshold in milliseconds before alerting.')
param llmLatencyThresholdMs int = 4000

var actionGroupName = toLower(format('{0}-{1}-alerts', baseName, environmentName))
var actionGroupShortName = toUpper(substring(actionGroupName, 0, min(12, length(actionGroupName))))

var emailReceiverDefinitions = [for email in emailReceivers: {
  name: replace(email, '@', '-at-')
  emailAddress: email
  useCommonAlertSchema: true
}]

resource actionGroup 'Microsoft.Insights/actionGroups@2022-06-01' = {
  name: actionGroupName
  location: 'Global'
  tags: tags
  properties: {
    groupShortName: actionGroupShortName
    enabled: true
    emailReceivers: emailReceiverDefinitions
    armRoleReceivers: [
      {
        name: 'subscription-owner'
        roleId: '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
        useCommonAlertSchema: true
      }
    ]
  }
}

resource hubAvailabilityAlert 'Microsoft.Insights/metricAlerts@2023-01-01' = {
  name: format('{0}-{1}-hub-availability', baseName, environmentName)
  location: location
  tags: tags
  properties: {
    description: 'Front Door origin health below expected threshold.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      frontDoorProfileId
    ]
    targetResourceType: 'microsoft.cdn/profiles'
    targetResourceRegion: 'global'
    autoMitigate: true
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      'allOf': [
        {
          name: 'OriginHealth'
          metricNamespace: 'Microsoft.Cdn/profiles'
          metricName: 'OriginHealthPercentage'
          operator: 'LessThan'
          threshold: hubAvailabilityThreshold
          timeAggregation: 'Average'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

var llmQuery = '''
let threshold = {0};
dependencies
| where timestamp > ago(15m)
| where type =~ "Http" and tostring(target) contains "llm"
| project timestamp, duration
| summarize AverageDuration = avg(duration) by bin(timestamp, 5m)
| where AverageDuration > threshold
'''

resource llmLatencyAlert 'Microsoft.Insights/scheduledQueryRules@2021-08-01' = {
  name: format('{0}-{1}-llm-latency', baseName, environmentName)
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    description: 'Average LLM dependency latency exceeds threshold.'
    severity: 2
    enabled: true
    autoMitigate: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    scopes: [
      workspaceId
    ]
    criteria: {
      allOf: [
        {
          query: format(llmQuery, string(llmLatencyThresholdMs))
          timeAggregation: 'Average'
          metricMeasureColumn: 'AverageDuration'
          operator: 'GreaterThan'
          threshold: llmLatencyThresholdMs
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        {
          actionGroupId: actionGroup.id
        }
      ]
    }
  }
}

output actionGroupId string = actionGroup.id
