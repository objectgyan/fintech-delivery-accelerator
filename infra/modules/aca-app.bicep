param name string
param location string
param environmentId string
param containerImage string
param tags object

resource app 'Microsoft.App/containerApps@2023-11-02-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
    }
    template: {
      containers: [
        {
          name: 'app'
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
    }
  }
}

output appName string = app.name
output url string = 'https://${app.properties.configuration.ingress.fqdn}'
