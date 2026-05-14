@description('Project + environment prefix, e.g. fintech-dev')
param namePrefix string

@description('Azure region')
param location string

@description('Email address that receives alerts')
param alertEmail string

@description('Application Insights resource ID (for request-failure alert)')
param appInsightsId string

@description('Service Bus namespace resource ID (for DLQ alert)')
param serviceBusNamespaceId string

@description('Tags applied to every resource in this module')
param tags object = {}

// Action Group — fan-out target for alert rules
resource ag 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${namePrefix}-ops-ag'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'fintechOps'
    enabled: true
    emailReceivers: [
      {
        name: 'oncall-email'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// Alert: Payment API 5xx rate — fires when App Insights records >=5 failed requests in 5 min
resource alertPayment5xx 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${namePrefix}-payment-5xx-rate'
  location: 'global'
  tags: tags
  properties: {
    description: 'Payment API server-error rate elevated — investigate POST /payments traffic + Stripe health'
    severity: 2
    enabled: true
    scopes: [appInsightsId]
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    targetResourceType: 'microsoft.insights/components'
    targetResourceRegion: location
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'failedRequests'
          metricNamespace: 'microsoft.insights/components'
          metricName: 'requests/failed'
          operator: 'GreaterThanOrEqual'
          threshold: 5
          timeAggregation: 'Count'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: ag.id
      }
    ]
  }
}

// Alert: Service Bus dead-letter depth — any message landing in DLQ is investigation-worthy
resource alertSbDlq 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${namePrefix}-sb-dlq-depth'
  location: 'global'
  tags: tags
  properties: {
    description: 'Service Bus dead-letter queue depth > 0 — message processing is failing on notification subscription'
    severity: 2
    enabled: true
    scopes: [serviceBusNamespaceId]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT15M'
    targetResourceType: 'Microsoft.ServiceBus/namespaces'
    targetResourceRegion: location
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'dlqDepth'
          metricNamespace: 'Microsoft.ServiceBus/namespaces'
          metricName: 'DeadletteredMessages'
          operator: 'GreaterThan'
          threshold: 0
          timeAggregation: 'Maximum'
          criterionType: 'StaticThresholdCriterion'
        }
      ]
    }
    actions: [
      {
        actionGroupId: ag.id
      }
    ]
  }
}

output actionGroupId string = ag.id
output payment5xxAlertId string = alertPayment5xx.id
output sbDlqAlertId string = alertSbDlq.id
