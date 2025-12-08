# Information Box – Modern IT Self-Service for Windows

Information Box is available as portable EXE builds from GitHub Releases (no MSIX required).

### Project Information
[![build](https://github.com/EvotecIT/InformationBox/actions/workflows/ci.yml/badge.svg)](https://github.com/EvotecIT/InformationBox/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/EvotecIT/InformationBox/branch/main/graph/badge.svg)](https://codecov.io/gh/EvotecIT/InformationBox)
[![release](https://img.shields.io/github/v/release/EvotecIT/InformationBox?display_name=tag)](https://github.com/EvotecIT/InformationBox/releases)
[![downloads](https://img.shields.io/github/downloads/EvotecIT/InformationBox/total.svg)](https://github.com/EvotecIT/InformationBox/releases)
[![top language](https://img.shields.io/github/languages/top/EvotecIT/InformationBox.svg)](https://github.com/EvotecIT/InformationBox)
[![code size](https://img.shields.io/github/languages/code-size/EvotecIT/InformationBox.svg)](https://github.com/EvotecIT/InformationBox)
[![license](https://img.shields.io/github/license/EvotecIT/InformationBox.svg)](https://github.com/EvotecIT/InformationBox)

### Author & Social
[![Twitter follow](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![Blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)
[![Threads](https://img.shields.io/badge/Threads-@PrzemyslawKlys-000000.svg?logo=Threads&logoColor=White)](https://www.threads.net/@przemyslaw.klys)
[![Discord](https://img.shields.io/discord/508328927853281280?style=flat-square&label=discord%20chat)](https://evo.yt/discord)

Modern, secret-free IT self-service desktop app for Windows. Shows device/account/network status, warns about password expiry, exposes tenant-aware quick links, and ships with built-in “Fix” actions for common end-user issues. Portable-friendly, multi-tenant, and themeable.

## Contents
- [Highlights](#highlights)
- [Build & Run](#build--run)
- [Deployment Flavors](#deployment-flavors)
- [Configuration Overview](#configuration-overview)
- [Branding](#branding)
- [Layout & Placement](#layout--placement)
- [Feature Flags](#feature-flags)
- [Links, Zones, and Local Sites](#links-zones-and-local-sites)
- [Fix Actions](#fix-actions)
- [Password Policy](#password-policy)
- [Tenant Overrides](#tenant-overrides)
- [Development & Tests](#development--tests)
- [License](#license)

## Highlights
- Cross-tenant, secret-free: works with Graph when available, degrades gracefully offline/LDAP.
- Built-in dense mode (default) for compact UI; configurable window size/placement per tenant.
- “Fix” tab ships with typed, AOT-friendly built-ins (OneDrive/Teams/VPN/Store/logs, etc.) that can be enabled/hidden/overridden via config.
- Themeable (Auto/Light/Dark/Classic/Ocean/Forest/Sunset) with white-label branding.
- Placeholder support in fix commands (`{{SUPPORT_EMAIL}}`, `{{COMPANY_NAME}}`, `{{PRODUCT_NAME}}`).
- Portable deployment: single-contained, single-fx, portable, and fx outputs from one script.

## Build & Run

```powershell
# Restore
dotnet restore

# Debug build
dotnet build InformationBox/InformationBox.csproj -c Debug

# Release publish (no MSIX)
dotnet publish InformationBox/InformationBox.csproj -c Release
```

## Deployment Flavors

`pwsh Build/Deploy.ps1` produces ready-to-ship artifacts in `Artefacts/`:

| Folder                | What you get                                   | Needs .NET? |
| --------------------- | ---------------------------------------------- | ----------- |
| `portable/`           | Self-contained, loose files                    | No          |
| `single-contained/`   | Self-contained, single-file style              | No          |
| `single-fx/`          | Framework-dependent, compressed single file    | Yes         |
| `fx/`                 | Framework-dependent, loose files (smallest)    | Yes         |

## Configuration Overview
- Load order: `--config <path>` (future) → `C:\ProgramData\InformationBox\config.json` → `%APPDATA%\InformationBox\config.json` → embedded `Assets/config.default.json`.
- User preferences (theme, etc.) persist in `%LOCALAPPDATA%\InformationBox\settings.json`.
- Dense mode is the default; all layout options are configurable.

### Sample skeleton
```json
{
  "branding": { "productName": "Information Box", "supportEmail": "support@example.com" },
  "layout": { "defaultWidth": 680, "defaultHeight": 440, "denseMode": true },
  "featureFlags": { "showLocalSites": true, "showContacts": true, "showHelp": true, "showHealth": false },
  "links": [],
  "zones": [],
  "localSites": [],
  "contacts": [],
  "passwordPolicy": { "onPremDays": 360, "cloudDays": 180 },
  "fixes": [],
  "tenantOverrides": {},
  "auth": { "clientId": "" }
}
```

## Branding

```json
"branding": {
  "productName": "Information Box",
  "companyName": "Evotec",
  "primaryColor": "#0050b3",
  "secondaryColor": "#e5f1ff",
  "logo": "Assets/logo.png",
  "logoWidth": 0,
  "logoHeight": 40,
  "icon": "Assets/app.ico",
  "theme": "Auto",
  "supportEmail": "support@example.com"
}
```

| Property         | Default           | Notes                                       |
| ---------------- | ----------------- | ------------------------------------------- |
| `productName`    | Information Box   | Window title / header text                  |
| `companyName`    | Evotec            | Shown under the header                      |
| `logo`           | Assets/logo.png   | PNG recommended; `logoWidth=0` auto-scales  |
| `logoHeight`     | 40                | Height in px                                |
| `icon`           | Assets/app.ico    | Window/taskbar icon                         |
| `theme`          | Auto              | Auto/Light/Dark/Classic/Ocean/Forest/Sunset |
| `supportEmail`   | support@contoso.com | Used by email/log collection actions      |

## Layout & Placement

```json
"layout": {
  "startMinimized": false,
  "defaultWidth": 680,
  "defaultHeight": 440,
  "horizontalAnchor": "Right",
  "verticalAnchor": "Bottom",
  "offsetX": 0,
  "offsetY": 0,
  "preferredCorner": "BottomRight",
  "multiMonitor": "Active",
  "trayOnly": false,
  "denseMode": true,
  "maxContentWidth": 0
}
```

| Property           | Default | Description                                      |
| ------------------ | ------- | ------------------------------------------------ |
| `defaultWidth`     | 680     | Initial window width                             |
| `defaultHeight`    | 440     | Initial window height                            |
| `horizontalAnchor` | Right   | Left / Center / Right                            |
| `verticalAnchor`   | Bottom  | Top / Center / Bottom                            |
| `offsetX` / `offsetY` | 0    | Pixel offsets from anchor                        |
| `multiMonitor`     | Active  | Active / Primary / DisplayIndex                  |
| `denseMode`        | true    | Tighter padding/spacing                          |
| `maxContentWidth`  | 0       | Cap content width (0 = no cap)                   |

## Feature Flags

```json
"featureFlags": {
  "showLocalSites": true,
  "showHelp": true,
  "showContacts": true,
  "showHealth": false
}
```

## Links, Zones, and Local Sites

```json
"links": [ { "name": "Create Ticket", "url": "https://helpdesk.example.com", "section": "Support", "visible": true, "order": 1 } ],
"zones": [ { "domain": "corp.example.com", "zone": "HQ" } ],
"localSites": [ { "label": "Intranet", "url": "https://intranet.example.com", "zone": "HQ", "visible": true, "order": 1 } ],
"contacts": [ { "label": "IT Service Desk", "email": "servicedesk@example.com", "phone": "+1-800-555-0100" } ]
```

- Zones resolve from `USERDNSDOMAIN`; Local Sites are auto-filtered by current zone.

## Fix Actions

Typed, override-friendly model. Leave `command` empty to reuse the built-in script; set `visible: false` to hide.

```json
"fixes": [
  {
    "id": "restart-onedrive",
    "name": "Restart OneDrive",
    "description": "Close and restart OneDrive sync client",
    "category": "OneDrive",
    "command": "",
    "confirm": "OneDrive will be restarted. Continue?",
    "visible": true,
    "order": 1
  }
]
```

Built-ins (override by `id`):

| ID                           | Description                                   |
| ---------------------------- | --------------------------------------------- |
| restart-onedrive             | Restart OneDrive sync client                  |
| reset-teams-cache            | Clear Microsoft Teams cache                   |
| clear-edge-cache             | Open Edge cache clear dialog                  |
| clear-chrome-cache           | Open Chrome cache clear dialog                |
| wsreset                      | Reset Microsoft Store cache                   |
| collect-logs                 | Collect diagnostics to Desktop zip            |
| email-logs                   | Collect logs, open mailto to support          |
| reset-vpn-adapter            | Toggle VPN adapters                           |
| repair-outlook-teams-addin   | Re-register Teams meeting add-in for Outlook  |

Placeholders inside commands:
- `{{SUPPORT_EMAIL}}`
- `{{COMPANY_NAME}}`
- `{{PRODUCT_NAME}}`

## Password Policy

```json
"passwordPolicy": {
  "onPremDays": 360,
  "cloudDays": 180
}
```

## Tenant Overrides

```json
"tenantOverrides": {
  "tenant-guid-here": {
    "branding": { "productName": "Client Portal", "supportEmail": "support@client.com" },
    "layout": { "defaultWidth": 720, "denseMode": true }
  }
}
```

## Development & Tests
- Target framework: `net10.0-windows` (WPF).
- Solution: `InformationBox.sln` (app) + `InformationBox.Tests` (xUnit).
- Run tests: `dotnet test InformationBox.Tests/InformationBox.Tests.csproj`.
- CI: `.github/workflows/ci.yml` (restore, build, test, coverage; Codecov upload optional via `CODECOV_TOKEN` secret).

### Project layout
```
InformationBox/
├─ Assets/            # logo, icon, default config
├─ Config/            # strongly-typed config records & enums
├─ Services/          # Graph/LDAP/device helpers
├─ Themes/            # XAML theme resources
├─ UI/                # ViewModels, commands, components
├─ App.xaml           # app entry
└─ MainWindow.xaml    # main shell
```

## License

Copyright (c) Evotec. Refer to the repository license information for details.
