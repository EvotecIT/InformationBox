# Information Box – Minimal Replacement Plan

Goal: rebuild the “Information Box” tray app 1:1 (same links/data) with a cleaner UI, minimal dependencies, and no client secrets. Target .NET 10, AOT-compatible, warnings-as-errors, XML docs enforced.

## Architecture
- Public-client MSAL with delegated `/me` (scopes: `User.Read`, `offline_access`), `https://login.microsoftonline.com/common` authority; no secrets on disk.
- Graph calls limited to `GET /me?$select=displayName,userPrincipalName,lastPasswordChangeDateTime,onPremisesSyncEnabled`.
- Config-driven: single JSON model (embedded default) with zones, password policy, links, feature flags, labels, branding; machine/user override files optional. JSON is the primary and only config format; CSV only used by an optional one-time importer.
- Native tenant detection (optional): `DsregGetJoinInfo` or registry (`HKLM\SOFTWARE\Microsoft\AzureAD\TenantInformation`) to prefill login hint.
- Tray app (WinUI 3 or WPF) with a single-window UI + tray icon; no external UI libs.

## Project setup
- SDK: `net10.0-windows10.0.19041.0` (AOT friendly). Enable PublishAot profile for release.
- Quality gates: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, nullable enabled, analyzers default.
- NuGet: `Microsoft.Identity.Client`, `Microsoft.Graph`, `CommunityToolkit.WinUI.Notifications` (if we want toast), nothing else unless necessary.
- Build configs: Debug (non-AOT), Release-AOT (self-contained, single-file optional), x64 only.

## Features to parity-match
- Show device/user info: device name, domain/tenant, zone header, IP, VPN/Wi-Fi label, password days left badge.
- Buttons/links: Help, Create Ticket, Service Portal/PowerApps, Local Site buttons from config; contacts/help text; progress/toast messages.
- Password age calc: use `lastPasswordChangeDateTime` from Graph `/me` (AAD/hybrid) or AD `pwdLastSet` via `NetUserGetInfo`/LDAP (domain-joined). No per-tenant config required for these lookups; policy days come from JSON config.
- Tray menu: Restore/Exit; optional “Sync data” action to re-read CSVs.

## Nice-to-have improvements (still minimal deps)
- Modern layout (two-column header + tile grid), system accent-aware light/dark, responsive for 125% DPI; allow white-label branding (logo, accent color) from config.
- Non-blocking device-code prompt with copy button; cache token silently thereafter.
- Lightweight health checks (latency to key URLs) gated behind a toggle; no extra permissions.

## Configuration model (JSON)
- Embedded default JSON resource; optional overrides at `C:\ProgramData\InformationBox\config.json`, `%APPDATA%\InformationBox\config.json`, or `--config path` (first found wins). JSON is authoritative; CSV importer is optional.
- Fields: `featureFlags`, `links` (name/url/icon/visible/order/section), `passwordPolicy`, `zones` (domain→zone), `localSites` (label/url/zone/visible/order), `contacts`, `branding` (logo path or data URI, primary/secondary colors, product name), `layout` (startMinimized, defaultWidth/Height, preferredCorner, multiMonitor: primary|active|displayIdx, trayOnly), `tenantOverrides` keyed by tenantId with partial overrides.
- Schema-first: strong typed records/enums (e.g., `PreferredCorner` enum, `FeatureFlag`), validated at startup; XML docs generated for all public types.
- Legacy CSV import helper (one-time) to populate JSON if old files exist.

## Deliverables
- `src/InformationBox.sln` with single app project.
- `config.sample.json` schema/example; embedded default config resource.
- `README.md` with app registration steps and publish commands (`dotnet publish -c Release-AOT -p:PublishAot=true`).
- `plan.md` (this file) to track execution.

## Open questions
- Choose UI stack: WinUI 3 (App SDK) vs WPF; WinUI 3 gives modern styling but adds Windows App SDK runtime. If we want zero runtime dependency, WPF is simpler.
- Should we include Intune/MDM compliance or stay offline-only?
- Any remaining legacy data we need to import once JSON is authoritative?
