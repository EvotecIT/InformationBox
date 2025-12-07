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
dotnet build InformationBox/InformationBox.csproj -c Debug

# portable Release (self-contained, single-file, ReadyToRun)
dotnet publish InformationBox/InformationBox.csproj -c Release
```

## Deploy script (multiple flavors)
`pwsh Build/Deploy.ps1` produces in `Artefacts/`:
- `portable/` (+zip): self-contained, loose files (runtime included).
- `single-contained/` (+zip): self-contained, single file with native self-extract (closest to legacy one-file).
- `single-fx/` (+zip): framework-dependent single file (compressed); requires .NET runtime on the machine.
- `fx/` (+zip): framework-dependent loose files; smallest unpacked set; runtime required.

Fix tab config schema:
```json
"fixes": [
  {
    "id": "restart-onedrive",          // optional; override built-in
    "name": "Restart OneDrive",
    "description": "Close and start OneDrive",
    "category": "OneDrive",
    "command": "",                     // empty => use built-in command
    "confirm": "OneDrive will be restarted. Continue?",
    "visible": true,
    "order": 1
  }
]
```
- Built-in actions (ids): `restart-onedrive`, `reset-teams-cache`, `clear-edge-cache`, `clear-chrome-cache`, `wsreset`, `collect-logs`.
- Add your own without an `id`, or override a built-in by matching `id` (you can change name/description/command/confirm/visible/order). Empty `command` reuses the built-in.
- `visible` toggles display; `order` controls sort; `confirm` shows an OK/Cancel dialog before running.

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

### Layout & window placement
`layout` section in the config controls footprint and position (device-independent pixels; DPI scaling will enlarge on high-DPI displays):
```json
"layout": {
  "startMinimized": false,
  "defaultWidth": 680,
  "defaultHeight": 440,
  "horizontalAnchor": "Right",   // Left | Center | Right
  "verticalAnchor": "Bottom",    // Top | Center | Bottom
  "offsetX": 0,
  "offsetY": 0,
  "preferredCorner": "BottomRight", // legacy fallback if anchors not set
  "multiMonitor": "Active",         // Active | Primary | DisplayIndex
  "trayOnly": false,
  "denseMode": true,                // tighter padding & font sizes
  "maxContentWidth": 0              // cap usable width on ultra-wide (0 = no cap)
}
```
- Anchors + offsets place the window relative to the work area; DPI scaling (e.g., 125%) multiplies the effective size, so pick slightly smaller defaults if you want a tighter footprint on high-DPI screens.
- `defaultWidth/Height` can be overridden per-tenant via `tenantOverrides`.

## Current state
- Config model, loader, and basic WPF shell scaffolded.
- Tenant detection and Graph/AD lookups to be added next.


ms-appx-web://microsoft.aad.brokerplugin/{b4d68965-5aaf-429f-95b3-c7fee3796706}
