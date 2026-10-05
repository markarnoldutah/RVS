// ──────────────────────────────────────────────────────────────
// Module: App Service Configuration
// ──────────────────────────────────────────────────────────────
// Applies app settings to an existing Web App and its optional
// staging deployment slot. Deployed after Key Vault and
// Application Insights to break circular dependencies.
// ──────────────────────────────────────────────────────────────
targetScope = 'resourceGroup'

// ── Parameters ────────────────────────────────────────────────

@description('Name of the existing Web App to configure.')
param appName string

@description('Application Insights connection string. Leave empty to skip.')
param appInsightsConnectionString string = ''

@description('Target environment name used to derive ASPNETCORE_ENVIRONMENT (staging = Staging, prod = Production).')
@allowed([
  'staging'
  'prod'
])
param environmentName string

@description('Key Vault URI for configuration provider. Leave empty to skip.')
param keyVaultUri string = ''

@description('Email sender address on the SendGrid-authenticated sending subdomain (DoNotReply@mail.rvintake.com in prod). Leave empty to skip — the API then refuses to build the email sender, which is the point: there is no safe default.')
param emailFromAddress string = ''

@description('Outbound SMS switch (#661). Always written, so the environment\'s state is explicit in its app settings rather than inherited from appsettings.json.')
param smsEnabled bool = false

@description('E.164 toll-free number in the environment\'s Twilio Messaging Service (#661). Leave empty to skip.')
param smsFromPhoneNumber string = ''

@description('Twilio Messaging Service SID (MG…). Not a secret. Leave empty to skip.')
param twilioMessagingServiceSid string = ''

@description('Public origin of the API (https://api.rvserviceflow.com). Twilio signs each webhook over the exact URL it called, and App Service terminates TLS in front of the app, so the API cannot rebuild that URL from the request. Leave empty to skip — the webhook then refuses every request.')
param twilioWebhookBaseUrl string = ''

@description('Display name on the email From line. Leave empty to keep the API default from appsettings.json ("RV Intake"); staging sets "RV Intake [Staging]" so its mail is easy to tell apart (#828).')
param emailSenderDisplayName string = ''

@description('When true, also applies settings to the staging deployment slot with ASPNETCORE_ENVIRONMENT=Staging.')
param configureStagingSlot bool = false

// ── Variables ──────────────────────────────────────────────────

var aspNetCoreEnvironment = environmentName == 'prod' ? 'Production' : 'Staging'

// Email (SendGrid) and SMS (Twilio). The secrets — SendGrid--ApiKey and Twilio--* —
// come from Key Vault through the configuration provider, not from here.
var messagingSettings = union(
  !empty(emailFromAddress)
    ? {
        Email__FromAddress: emailFromAddress
      }
    : {},
  !empty(emailSenderDisplayName)
    ? {
        Email__SenderDisplayName: emailSenderDisplayName
      }
    : {},
  {
    Sms__Enabled: string(smsEnabled)
  },
  !empty(smsFromPhoneNumber)
    ? {
        Sms__FromPhoneNumber: smsFromPhoneNumber
      }
    : {},
  !empty(twilioMessagingServiceSid)
    ? {
        Twilio__MessagingServiceSid: twilioMessagingServiceSid
      }
    : {},
  !empty(twilioWebhookBaseUrl)
    ? {
        Twilio__WebhookBaseUrl: twilioWebhookBaseUrl
      }
    : {}
)

// ── Resources ─────────────────────────────────────────────────

resource webApp 'Microsoft.Web/sites@2024-11-01' existing = {
  name: appName
}

resource appSettings 'Microsoft.Web/sites/config@2024-11-01' = {
  parent: webApp
  name: 'appsettings'
  properties: union(
    {
      ASPNETCORE_ENVIRONMENT: aspNetCoreEnvironment
    },
    !empty(appInsightsConnectionString)
      ? {
          APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
        }
      : {},
    !empty(keyVaultUri)
      ? {
          KeyVault__VaultUri: keyVaultUri
        }
      : {},
    messagingSettings
  )
}

// ── Staging Slot Settings ─────────────────────────────────────

resource stagingSlot 'Microsoft.Web/sites/slots@2024-11-01' existing = if (configureStagingSlot) {
  parent: webApp
  name: 'staging'
}

resource stagingSlotAppSettings 'Microsoft.Web/sites/slots/config@2024-11-01' = if (configureStagingSlot) {
  parent: stagingSlot
  name: 'appsettings'
  properties: union(
    {
      ASPNETCORE_ENVIRONMENT: 'Staging'
    },
    !empty(appInsightsConnectionString)
      ? {
          APPLICATIONINSIGHTS_CONNECTION_STRING: appInsightsConnectionString
        }
      : {},
    !empty(keyVaultUri)
      ? {
          KeyVault__VaultUri: keyVaultUri
        }
      : {},
    messagingSettings
  )
}
