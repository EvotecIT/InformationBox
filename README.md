# Information Box (next-gen)

Cross-tenant, secret-free replacement for the legacy “Information Box” tray app.

## Build targets
- `net10.0-windows10.0.19041.0`
- WPF, AOT-friendly. Warnings-as-errors, XML docs on.

## Restore / build
> Note: WPF targeting Windows requires building on Windows or with Windows targeting packs.
```bash
# restore
dotnet restore

# build (Debug)
dotnet build src/InformationBox/InformationBox.csproj -c Debug

# publish self-contained + AOT (Release)
dotnet publish src/InformationBox/InformationBox.csproj -c Release -p:PublishAot=true -r win-x64 --self-contained true
```

## Config
- Embedded default: `Assets/config.default.json`.
- Override search order: `--config <path>` (future), `C:\ProgramData\InformationBox\config.json`, `%APPDATA%\InformationBox\config.json`.
- Sample: `config.sample.json` in repo root.

### Azure AD app registration
The Graph client ID in your config must map to a public-client app registration that supports brokered Windows SSO.

1. In Azure Portal → Azure Active Directory → App registrations, create/open the app that matches `auth.clientId`.
2. Under **Authentication**:
   - Tick **Allow public client flows**.
   - Add these redirect URIs under “Mobile and desktop applications”:
     - `ms-appx-web://microsoft.aad.brokerplugin/{clientId}` (replace `{clientId}` with your actual GUID).
     - `https://login.microsoftonline.com/common/oauth2/nativeclient`.
3. Under **API permissions**, add **Microsoft Graph → Delegated → User.Read** and grant admin consent.

Without those settings the broker dialog fails with `AADSTS500113` and the app falls back to LDAP.

## Current state
- Config model, loader, and basic WPF shell scaffolded.
- Tenant detection and Graph/AD lookups to be added next.


ms-appx-web://microsoft.aad.brokerplugin/{b4d68965-5aaf-429f-95b3-c7fee3796706}
