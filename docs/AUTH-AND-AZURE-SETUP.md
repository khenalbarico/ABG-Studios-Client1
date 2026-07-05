# Auth & Azure Setup Guide (walkthrough checklist)

This is the step-by-step list we will go through **together, interactively**
(CLAUDE.md §7). Nothing here is wired until you complete these steps —
the code is already prepared to use the results. Do DEV first, then repeat
for PROD with the PROD resources.

## 1. Azure resources (per environment)

| Resource | DEV | PROD |
|---|---|---|
| Static Web App (Standard tier — required for linked backend + custom auth) | ☐ | ☐ |
| Function App on **Flex Consumption** | ☐ | ☐ |
| Storage account (Table Storage) | ☐ | ☐ |

1. Create the storage account; copy its **connection string** into the
   Function App setting `TablesConnectionString` and into the seeder's
   `appsettings.{Development|Production}.json`.
2. Function App settings to add (Configuration → Application settings):
   - `TablesConnectionString`
   - `Firebase:DatabaseUrl` — your Realtime DB root URL
   - `Firebase:AuthToken` — database secret (or leave empty if rules allow)
   - `Paymongo:SecretKey` — `sk_test_…` on DEV, `sk_live_…` on PROD
   - `Paymongo:WebhookSecretKey` — from step 4
3. In the SWA: **Settings → APIs → Link** the environment's Function App.
   This is what makes `/api/*` same-origin and forwards the signed-in
   user's `x-ms-client-principal` header to the Functions backend.

## 2. Google login

1. Go to https://console.cloud.google.com/ → create project `abg-studios`.
2. **APIs & Services → OAuth consent screen**: External, app name
   "ABG Studios", add your support email, publish.
3. **Credentials → Create credentials → OAuth client ID → Web application**:
   - Authorized redirect URI (one per environment):
     `https://<your-swa-hostname>/.auth/login/google/callback`
4. Copy the **Client ID** and **Client Secret** into the SWA's
   **Configuration → Application settings** as:
   - `GOOGLE_CLIENT_ID`
   - `GOOGLE_CLIENT_SECRET`
   (These names are referenced by `staticwebapp.config.json`.)

## 3. Facebook login

1. Go to https://developers.facebook.com/ → Create App → type **Consumer**.
2. Add the **Facebook Login** product → Web.
3. Settings → Basic: copy **App ID** and **App Secret**.
4. Facebook Login → Settings → Valid OAuth Redirect URIs:
   `https://<your-swa-hostname>/.auth/login/facebook/callback`
5. Add to the SWA Application settings:
   - `FACEBOOK_APP_ID`
   - `FACEBOOK_APP_SECRET`
6. Switch the Facebook app to **Live** mode when PROD is ready.

## 4. Paymongo webhook (per environment)

Register a webhook pointing at the Function App (via the SWA hostname so
it stays same-origin, or the Function App hostname directly):

```
POST https://api.paymongo.com/v1/webhooks
Authorization: Basic base64(sk_..._key + ":")
{
  "data": { "attributes": {
    "url": "https://<host>/api/webhooks/paymongo",
    "events": ["payment.paid", "payment.failed", "qrph.expired"]
  } }
}
```

The response contains `attributes.secret_key` — put it in the Function
App setting `Paymongo:WebhookSecretKey`. Unsigned or mis-signed webhook
calls are rejected with 401.

## 5. GitHub deployment secrets

UI repo (`ABG-Studios-UI1`):
- `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV` / `_PROD` — SWA deployment tokens
- `CROSS_REPO_TOKEN` — PAT with read access to `ABG-Studios-API1`
  (the UI build checks out the API repo for the shared domain library)

API repo (`ABG-Studios-API1`):
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE_DEV` / `_PROD` — publish profiles
- Repo variables `FUNCTIONAPP_NAME_DEV` / `_PROD`

## 6. Seed metadata

```
cd ABG-Studios-API1/Abg.Seeder
# copy data/services.sample.json -> data/services.json and edit
# copy data/schedulecfg.sample.json -> data/schedulecfg.json and edit
dotnet run -- --env Development --data data --dry-run   # review
dotnet run -- --env Development --data data             # write
```

## 7. Smoke test per environment

1. Open the SWA URL → services render (catalog call works).
2. Sign in with Google → book a slot → QR appears with 3:00 countdown.
3. Pay with a Paymongo test QR → screen flips to success and shows the
   proof-of-purchase QR (encodes the booking ID only).
4. Re-scan of the booking ID on the admin side finds the record in the
   `Bookings` table (PartitionKey = booking ID).
