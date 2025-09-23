targetScope = 'resourceGroup'

@description('Resource ID of the Log Analytics workspace that should receive diagnostics.')
param workspaceId string

@description('App Service name for diagnostic settings.')
param appServiceName string

@description('SignalR name for diagnostic settings.')
param signalRName string

@existing
resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: appServiceName
}

@existing
resource signalR 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: signalRName
}

resource appServiceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'appservice-to-loganalytics'
  scope: appService
  properties: {
    workspaceId: workspaceId
    logs: [
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource signalRDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'signalr-to-loganalytics'
  scope: signalR
  properties: {
    workspaceId: workspaceId
    logs: [
      {
        category: 'ConnectivityLogs'
        enabled: true
      }
      {
        category: 'MessagingLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}
