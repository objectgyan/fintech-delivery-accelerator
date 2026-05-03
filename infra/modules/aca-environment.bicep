param name string
param location string
param logAnalyticsWorkspaceId string
param logAnalyticsCustomerId string
param tags object

resource environment 'Microsoft.App/managedEnvironments@2023-11-02-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
  }
}

output environmentId string = environment.id
output environmentName string = environment.name
