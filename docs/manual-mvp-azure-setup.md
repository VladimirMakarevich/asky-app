# Manual Azure Setup For Asky MVP Validation

The goal of this guide is to create only the minimum Azure resources required to stand up and test the MVP (backend + SignalR + Speech + secrets + monitoring) before the production-grade infrastructure pipeline is in place.

## Prerequisites

- Azure subscription with rights to create resource groups and services
- Azure CLI 2.52+ installed locally and logged in (`az login`)
- .NET 9 SDK and tooling installed to run the backend locally or to deploy it into App Service
- SignalR and Speech SDK keys will be surfaced manually via Azure Portal/CLI and copied into the backend configuration

> **Naming convention:** Replace `asky` with your chosen project prefix. Throughout the guide we will use `asky-dev` as the environment suffix.

---

## 1. Create Resource Group

```bash
az group create \
  --name asky-dev-rg \
  --location westeurope
```

All subsequent resources should target this resource group.

---

## 2. Provision Azure Speech Service

```bash
az cognitiveservices account create \
  --name askydevspeech \
  --resource-group asky-dev-rg \
  --kind SpeechServices \
  --sku S0 \
  --location westeurope \
  --yes
```

Retrieve keys and endpoint:

```bash
az cognitiveservices account keys list \
  --name askydevspeech \
  --resource-group asky-dev-rg

az cognitiveservices account show \
  --name askydevspeech \
  --resource-group asky-dev-rg \
  --query properties.endpoint
```

Store the primary key and endpoint for later use in Key Vault / app settings.

---

## 3. Create Azure SignalR Service (Serverless tier)

```bash
az signalr create \
  --name askydevsignalr \
  --resource-group asky-dev-rg \
  --sku Standard_S1 \
  --unit-count 1 \
  --service-mode Serverless \
  --location westeurope
```

Fetch connection string:

```bash
az signalr key list \
  --name askydevsignalr \
  --resource-group asky-dev-rg \
  --query primaryConnectionString \
  --output tsv
```

---

## 4. Set Up App Service Plan & Web App (Linux)

```bash
az appservice plan create \
  --name asky-dev-plan \
  --resource-group asky-dev-rg \
  --location westeurope \
  --sku P1v3 \
  --is-linux

az webapp create \
  --name asky-dev-api \
  --plan asky-dev-plan \
  --resource-group asky-dev-rg \
  --runtime "DOTNET|9.0"
```

> If you only need to run the backend locally, you can skip the `webapp` creation. Keep the plan/site if you plan to deploy and test from the cloud.

Configure application settings (replace values with the secrets gathered above):

```bash
az webapp config appsettings set \
  --name asky-dev-api \
  --resource-group asky-dev-rg \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    AzureSpeech__SubscriptionKey=<speech-key> \
    AzureSpeech__Region=westeurope \
    SignalR__PrimaryConnectionString="<signalr-connection-string>" \
    ApplicationInsights__ConnectionString=<app-insights-connection-string>
```

(We will create Application Insights in a later step; you can re-run this command to inject the connection string once you have it.)

---

## 5. Deploy Key Vault For Secrets (Optional but recommended)

If you prefer to avoid putting secrets directly into App Service settings, create a Key Vault and store the Speech key + SignalR connection string.

```bash
az keyvault create \
  --name asky-dev-kv \
  --resource-group asky-dev-rg \
  --location westeurope \
  --enable-rbac-authorization true

az keyvault secret set \
  --vault-name asky-dev-kv \
  --name speech-subscription-key \
  --value <speech-key>

az keyvault secret set \
  --vault-name asky-dev-kv \
  --name signalr-primary-connection \
  --value "<signalr-connection-string>"
```

Assign the Web App’s managed identity (or your user when running locally) `Key Vault Secrets User` role to access the secrets:

```bash
WEBAPP_ID=$(az webapp show -n asky-dev-api -g asky-dev-rg --query identity.principalId -o tsv)
az role assignment create \
  --assignee $WEBAPP_ID \
  --role "Key Vault Secrets User" \
  --scope $(az keyvault show --name asky-dev-kv --query id -o tsv)
```

---

## 6. Create Application Insights (with Workspace)

```bash
az monitor log-analytics workspace create \
  --resource-group asky-dev-rg \
  --workspace-name asky-dev-law \
  --location westeurope

az monitor app-insights component create \
  --app asky-dev-appi \
  --location westeurope \
  --resource-group asky-dev-rg \
  --kind web \
  --workspace asky-dev-law
```

Grab the connection string:

```bash
az monitor app-insights component show \
  --app asky-dev-appi \
  --resource-group asky-dev-rg \
  --query connectionString \
  --output tsv
```

Apply to App Service settings (see step 4) and/or use locally via `ApplicationInsights__ConnectionString`.

---

## 7. Update Backend Configuration (Local Run)

When running the backend locally (without App Service), set environment variables or use a `appsettings.Development.json` override:

```json
{
  "AzureSpeech": {
    "SubscriptionKey": "<speech-key>",
    "Region": "westeurope"
  },
  "SignalR": {
    "PrimaryConnectionString": "<signalr-connection-string>"
  },
  "ApplicationInsights": {
    "ConnectionString": "<app-insights-connection-string>"
  }
}
```

Alternatively, if you stored secrets in Key Vault, wire the `DefaultAzureCredential` in your configuration pipeline and set the vault name.

---

## 8. Deploy & Smoke Test

### Option A – Deploy to App Service

```bash
dotnet publish backend/AskyBackend.csproj -c Release -o out
az webapp deploy \
  --resource-group asky-dev-rg \
  --name asky-dev-api \
  --type zip \
  --src-path out
```

Verify the service responds:

```bash
curl https://asky-dev-api.azurewebsites.net/healthz
```

### Option B – Run Locally With Cloud Dependencies

1. Ensure the backend `appsettings.Development.json` contains the Speech & SignalR connection data.
2. Run `dotnet run --project backend/AskyBackend.csproj`.
3. Point the mobile app / SignalR client to `http://localhost:5267` (or the port shown in console).

---

## 9. Clean-Up (After MVP Test)

Once testing is complete, delete the resource group to avoid costs:

```bash
az group delete --name asky-dev-rg --yes --no-wait
```

---

## Summary Checklist

- [ ] Resource group created
- [ ] Speech Service provisioned & key captured
- [ ] SignalR Service provisioned & connection string captured
- [ ] (Optional) Key Vault storing secrets and access granted
- [ ] App Service (Plan + Web App) deployed or backend running locally with cloud dependencies
- [ ] Application Insights hooked up for telemetry
- [ ] Backend configured (Environment variables / appsettings) with the above values

Following this lightweight deployment enables end-to-end validation of the MVP without waiting on the full Infrastructure-as-Code pipeline.
