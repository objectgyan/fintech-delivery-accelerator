targetScope = 'subscription'

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Azure region')
param location string = 'eastus'

@description('Project prefix for resource naming')
param projectPrefix string = 'fintech'

var resourceGroupName = '${projectPrefix}-${environment}-rg'
var tags = {
  project: 'fintech-delivery-accelerator'
  environment: environment
  managedBy: 'bicep'
}

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  scope: rg
  params: {
    name: '${projectPrefix}-${environment}-logs'
    location: location
    tags: tags
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'app-insights'
  scope: rg
  params: {
    name: '${projectPrefix}-${environment}-ai'
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    tags: tags
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  scope: rg
  params: {
    name: '${replace(projectPrefix, '-', '')}${environment}acr'
    location: location
    tags: tags
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  scope: rg
  params: {
    name: '${projectPrefix}-${environment}-kv'
    location: location
    tags: tags
  }
}

module serviceBus 'modules/service-bus.bicep' = {
  name: 'service-bus'
  scope: rg
  params: {
    name: '${projectPrefix}-${environment}-bus'
    location: location
    tags: tags
  }
}

// SQL server added in Phase 1 (FT-101) — not needed for Phase 0 placeholder health check

module acaEnv 'modules/aca-environment.bicep' = {
  name: 'aca-env'
  scope: rg
  params: {
    name: '${projectPrefix}-${environment}-aca-env'
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    tags: tags
  }
}

module acaApp 'modules/aca-app.bicep' = {
  name: 'aca-app'
  scope: rg
  params: {
    name: '${projectPrefix}-${environment}-placeholder'
    location: location
    environmentId: acaEnv.outputs.environmentId
    containerImage: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
    tags: tags
  }
}

module acaPayment 'modules/aca-app.bicep' = {
  name: 'aca-payment'
  scope: rg
  params: {
    name: '${projectPrefix}-${environment}-payment'
    location: location
    environmentId: acaEnv.outputs.environmentId
    containerImage: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
    ingressExternal: true
    minReplicas: 0
    maxReplicas: 3
    tags: tags
    envVars: [
      { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
      { name: 'ServiceBus__TopicName', value: 'payment-authorized' }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
    ]
  }
}

module acaNotification 'modules/aca-worker.bicep' = {
  name: 'aca-notification'
  scope: rg
  params: {
    name: '${projectPrefix}-${environment}-notification'
    location: location
    environmentId: acaEnv.outputs.environmentId
    containerImage: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'
    minReplicas: 0
    maxReplicas: 3
    tags: tags
    envVars: [
      { name: 'ServiceBus__TopicName', value: 'payment-authorized' }
      { name: 'ServiceBus__SubscriptionName', value: 'notification' }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.outputs.connectionString }
    ]
  }
}

output resourceGroupName string = resourceGroupName
output acrLoginServer string = acr.outputs.loginServer
output acrName string = acr.outputs.name
output acaAppName string = acaApp.outputs.appName
output acaUrl string = acaApp.outputs.url
output acaPaymentAppName string = acaPayment.outputs.appName
output acaPaymentUrl string = acaPayment.outputs.url
output acaNotificationAppName string = acaNotification.outputs.appName
output appInsightsConnectionString string = appInsights.outputs.connectionString
output keyVaultName string = keyVault.outputs.name
output serviceBusNamespace string = serviceBus.outputs.namespaceName
output serviceBusTopicName string = serviceBus.outputs.topicName
