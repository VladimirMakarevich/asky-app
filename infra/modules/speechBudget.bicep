targetScope = 'subscription'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Resource ID of the Speech service to monitor cost for.')
param speechResourceId string

@description('Action group ID that should receive budget notifications.')
param actionGroupId string

@description('Monthly budget amount in the billing currency.')
param monthlyBudgetAmount int = 500

@description('Optional contacts in addition to action group for budget alerts.')
param additionalEmails array = []

var budgetName = toLower(format('{0}-{1}-speech-budget', baseName, environmentName))

resource speechBudget 'Microsoft.Consumption/budgets@2021-10-01' = {
  name: budgetName
  properties: {
    category: 'Cost'
    amount: monthlyBudgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: '2024-01-01T00:00:00Z'
      endDate: '2030-12-31T00:00:00Z'
    }
    notifications: {
      actual80Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactGroups: [
          actionGroupId
        ]
        contactEmails: additionalEmails
      }
      actual100Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Actual'
        contactGroups: [
          actionGroupId
        ]
        contactEmails: additionalEmails
      }
    }
    filter: {
      dimensions: [
        {
          name: 'ResourceId'
          operator: 'In'
          values: [
            speechResourceId
          ]
        }
      ]
    }
  }
}
