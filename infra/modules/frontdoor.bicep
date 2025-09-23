targetScope = 'resourceGroup'

@description('Base prefix shared across all environments.')
param baseName string

@description('Environment identifier (dev/stage/prod).')
param environmentName string

@description('Tags propagated to Front Door resources.')
param tags object

@description('Host name of the App Service origin (e.g. azurewebsites.net).')
param originHostName string

@description('Optional configuration overrides for Front Door and custom domains.')
param frontDoorConfig object = {}

var profileName = contains(frontDoorConfig, 'profileName') ? frontDoorConfig.profileName : toLower(format('{0}-{1}-afd', baseName, environmentName))
var endpointName = contains(frontDoorConfig, 'endpointName') ? frontDoorConfig.endpointName : toLower(format('{0}-{1}-endpoint', baseName, environmentName))
var originGroupName = contains(frontDoorConfig, 'originGroupName') ? frontDoorConfig.originGroupName : toLower(format('{0}-{1}-og', baseName, environmentName))
var originName = contains(frontDoorConfig, 'originName') ? frontDoorConfig.originName : toLower(format('{0}-{1}-origin', baseName, environmentName))
var routeName = contains(frontDoorConfig, 'routeName') ? frontDoorConfig.routeName : toLower(format('{0}-{1}-route', baseName, environmentName))
var healthProbePath = contains(frontDoorConfig, 'healthProbePath') ? frontDoorConfig.healthProbePath : '/healthz'
var patternsToMatch = contains(frontDoorConfig, 'patternsToMatch') ? frontDoorConfig.patternsToMatch : [ '/*' ]
var customDomainHostName = contains(frontDoorConfig, 'customDomainHostName') ? frontDoorConfig.customDomainHostName : ''
var minimumTlsVersion = contains(frontDoorConfig, 'minimumTlsVersion') ? frontDoorConfig.minimumTlsVersion : 'TLS1_2'
var useManagedCertificate = contains(frontDoorConfig, 'useManagedCertificate') ? frontDoorConfig.useManagedCertificate : true

var hasCustomDomain = customDomainHostName != ''
var customDomainName = hasCustomDomain ? replace(customDomainHostName, '.', '-') : ''

resource frontDoorProfile 'Microsoft.Cdn/profiles@2023-05-01' = {
  name: profileName
  location: 'Global'
  sku: {
    name: 'Standard_AzureFrontDoor'
  }
  tags: tags
}

resource frontDoorEndpoint 'Microsoft.Cdn/profiles/endpoints@2023-05-01' = {
  parent: frontDoorProfile
  name: endpointName
  location: 'Global'
  properties: {
    enabledState: 'Enabled'
  }
  tags: tags
}

resource frontDoorOriginGroup 'Microsoft.Cdn/profiles/originGroups@2023-05-01' = {
  parent: frontDoorProfile
  name: originGroupName
  properties: {
    sessionAffinityState: 'Disabled'
    healthProbeSettings: {
      probePath: healthProbePath
      probeRequestType: 'GET'
      probeProtocol: 'Https'
      probeIntervalInSeconds: 120
    }
    loadBalancingSettings: {
      sampleSize: 4
      successfulSamplesRequired: 3
      additionalLatencyInMilliseconds: 0
    }
  }
  tags: tags
}

resource frontDoorOrigin 'Microsoft.Cdn/profiles/originGroups/origins@2023-05-01' = {
  parent: frontDoorOriginGroup
  name: originName
  properties: {
    hostName: originHostName
    httpPort: 80
    httpsPort: 443
    originHostHeader: originHostName
    priority: 1
    weight: 1000
    enabledState: 'Enabled'
  }
}

resource frontDoorRoute 'Microsoft.Cdn/profiles/endpoints/routes@2023-05-01' = {
  parent: frontDoorEndpoint
  name: routeName
  properties: {
    originGroup: {
      id: frontDoorOriginGroup.id
    }
    supportedProtocols: [
      'Https'
    ]
    linkToDefaultDomain: 'Enabled'
    httpsRedirect: 'Enabled'
    patternsToMatch: patternsToMatch
    forwardingProtocol: 'HttpsOnly'
    enabledState: 'Enabled'
    caching: {
      cacheDuration: '0:0:0'
      queryParameters: {
        includeQueryString: true
        queryParameters: []
      }
    }
  }
}

resource frontDoorCustomDomain 'Microsoft.Cdn/profiles/customDomains@2023-05-01' = if (hasCustomDomain) {
  parent: frontDoorProfile
  name: customDomainName
  properties: {
    hostName: customDomainHostName
    tlsSettings: {
      certificateType: useManagedCertificate ? 'ManagedCertificate' : 'CustomerCertificate'
      minimumTlsVersion: minimumTlsVersion
    }
  }
  tags: tags
}

resource frontDoorCustomDomainAssociation 'Microsoft.Cdn/profiles/endpoints/routes@2023-05-01' = if (hasCustomDomain) {
  parent: frontDoorEndpoint
  name: format('{0}-custom', routeName)
  properties: {
    originGroup: {
      id: frontDoorOriginGroup.id
    }
    customDomains: [
      {
        id: frontDoorCustomDomain.id
      }
    ]
    supportedProtocols: [
      'Https'
    ]
    linkToDefaultDomain: 'Disabled'
    httpsRedirect: 'Enabled'
    patternsToMatch: patternsToMatch
    forwardingProtocol: 'HttpsOnly'
    enabledState: 'Enabled'
    caching: {
      cacheDuration: '0:0:0'
      queryParameters: {
        includeQueryString: true
        queryParameters: []
      }
    }
  }
}

output frontDoorHost string = frontDoorEndpoint.properties.hostName
output customDomainHost string = hasCustomDomain ? customDomainHostName : ''
output profileId string = frontDoorProfile.id
