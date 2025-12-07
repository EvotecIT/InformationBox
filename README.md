# Information Box

Cross-tenant, secret-free IT information and self-service app for Windows.

Developed by **Evotec**.

## Features

- Display device, network, and account information
- Password expiration status (via Graph API or LDAP)
- Quick links to IT resources and support portals
- Self-service fix actions (restart OneDrive, clear caches, etc.)
- Multi-tenant support with tenant-specific overrides
- Themeable UI with 6 built-in themes
- Fully customizable branding for client deployments

## Build

> **Note**: WPF targeting Windows requires building on Windows or with Windows targeting packs.

```bash
# Restore dependencies
dotnet restore

# Build (Debug)
dotnet build InformationBox/InformationBox.csproj -c Debug

# Build (Release)
dotnet publish InformationBox/InformationBox.csproj -c Release
```

## Deploy Script

`pwsh Build/Deploy.ps1` produces multiple deployment flavors in `Artefacts/`:

| Flavor              | Description                                          | Runtime Required |
| ------------------- | ---------------------------------------------------- | ---------------- |
| `portable/`         | Self-contained, loose files                          | No               |
| `single-contained/` | Self-contained, single file with native self-extract | No               |
| `single-fx/`        | Framework-dependent, single compressed file          | Yes              |
| `fx/`               | Framework-dependent, loose files (smallest)          | Yes              |

---

## Configuration

### Config File Locations

Configuration is loaded in this priority order:

1. Command line: `--config <path>` (future)
2. System-wide: `C:\ProgramData\InformationBox\config.json`
3. User: `%APPDATA%\InformationBox\config.json`
4. Embedded default: `Assets/config.default.json`

### User Settings

User preferences (like theme selection) are stored separately and persist across sessions:
- Location: `%LOCALAPPDATA%\InformationBox\settings.json`
- These override config defaults but can be changed at runtime

---

## Configuration Reference

### Branding

Customize the application appearance and identity for your organization or clients.

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
  "theme": "Light"
}
```

| Property         | Type   | Default           | Description                                     |
| ---------------- | ------ | ----------------- | ----------------------------------------------- |
| `productName`    | string | "Information Box" | Window title and header text                    |
| `companyName`    | string | "Evotec"          | Company/vendor name                             |
| `primaryColor`   | string | "#0050b3"         | Primary accent color (hex)                      |
| `secondaryColor` | string | "#e5f1ff"         | Secondary accent color (hex)                    |
| `logo`           | string | null              | Path to logo image (relative, absolute, or URL) |
| `logoWidth`      | int    | 0                 | Logo width in pixels (0 = auto based on height) |
| `logoHeight`     | int    | 32                | Logo height in pixels                           |
| `icon`           | string | null              | Path to window/taskbar icon (.ico)              |
| `theme`          | string | "Light"           | Default UI theme                                |

### Themes

Available built-in themes:

| Theme     | Description                           |
| --------- | ------------------------------------- |
| `Light`   | Clean light theme with blue accents   |
| `Dark`    | Dark theme for low-light environments |
| `Classic` | Traditional gray tones                |
| `Ocean`   | Deep blue tones                       |
| `Forest`  | Deep green tones                      |
| `Sunset`  | Warm orange/purple tones              |

Users can change themes at runtime via the dropdown in the footer. Their preference is saved automatically.

### Feature Flags

Control which features are visible in the UI.

```json
"featureFlags": {
  "showLocalSites": true,
  "showHelp": true,
  "showContacts": true,
  "showHealth": false
}
```

| Flag             | Default | Description                          |
| ---------------- | ------- | ------------------------------------ |
| `showLocalSites` | true    | Show local site links in Support tab |
| `showHelp`       | true    | Show help resources                  |
| `showContacts`   | true    | Show contact cards in Support tab    |
| `showHealth`     | false   | Show password health indicators      |

### Layout & Window Placement

Control window size, position, and density.

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

| Property           | Type   | Default       | Description                                            |
| ------------------ | ------ | ------------- | ------------------------------------------------------ |
| `startMinimized`   | bool   | false         | Start minimized to tray                                |
| `defaultWidth`     | int    | 680           | Window width in pixels                                 |
| `defaultHeight`    | int    | 440           | Window height in pixels                                |
| `horizontalAnchor` | string | "Right"       | Horizontal position: `Left`, `Center`, `Right`         |
| `verticalAnchor`   | string | "Bottom"      | Vertical position: `Top`, `Center`, `Bottom`           |
| `offsetX`          | int    | 0             | Horizontal offset from anchor                          |
| `offsetY`          | int    | 0             | Vertical offset from anchor                            |
| `preferredCorner`  | string | "BottomRight" | Legacy fallback if anchors not set                     |
| `multiMonitor`     | string | "Active"      | Monitor selection: `Active`, `Primary`, `DisplayIndex` |
| `trayOnly`         | bool   | false         | Run as tray-only application                           |
| `denseMode`        | bool   | true          | Tighter padding and font sizes                         |
| `maxContentWidth`  | int    | 0             | Cap content width (0 = no cap)                         |

### Password Policy

Configure password expiration thresholds.

```json
"passwordPolicy": {
  "onPremDays": 360,
  "cloudDays": 180
}
```

### Links

Define quick-access links shown in the Support tab.

```json
"links": [
  {
    "name": "Create Ticket",
    "url": "https://helpdesk.example.com",
    "section": "Support",
    "visible": true,
    "order": 1
  }
]
```

### Zones

Map user domains to zone identifiers for location-specific content.

```json
"zones": [
  {
    "domain": "corp.example.com",
    "zone": "HQ"
  },
  {
    "domain": "branch.example.com",
    "zone": "Branch-US"
  }
]
```

### Local Sites

Zone-specific local resources (filtered by user's zone).

```json
"localSites": [
  {
    "label": "Intranet",
    "url": "https://intranet.example.com",
    "zone": "HQ",
    "visible": true,
    "order": 1
  }
]
```

### Contacts

Support contact cards shown in the Support tab.

```json
"contacts": [
  {
    "label": "IT Service Desk",
    "email": "servicedesk@example.com",
    "phone": "+1-800-555-0100"
  }
]
```

### Fix Actions

Self-service troubleshooting actions shown in the Fix tab.

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

**Built-in fix actions** (use these IDs to override):
- `restart-onedrive` - Restart OneDrive sync client
- `reset-teams-cache` - Clear Microsoft Teams cache
- `clear-edge-cache` - Clear Microsoft Edge browser cache
- `clear-chrome-cache` - Clear Google Chrome browser cache
- `wsreset` - Reset Windows Store cache
- `collect-logs` - Collect diagnostic logs

**Custom actions**: Omit `id` to add new actions, or match an existing `id` to override built-in behavior.

### Tenant Overrides

Apply configuration overrides for specific Azure AD tenants.

```json
"tenantOverrides": {
  "tenant-guid-here": {
    "branding": {
      "productName": "Client Portal",
      "primaryColor": "#ff6600"
    },
    "layout": {
      "defaultWidth": 800
    }
  }
}
```

### Authentication

Configure Azure AD app registration for Graph API access.

```json
"auth": {
  "clientId": "your-app-registration-guid"
}
```

---

## Client Branding & White-Labeling

Information Box supports full white-labeling for client deployments. Each client can have their own branding while using the same codebase.

### Assets Structure

```
InformationBox/Assets/
├── app.ico              # Application icon (window, taskbar)
├── logo.png             # Company logo (displayed in header)
└── config.default.json  # Default configuration
```

### Creating a Client Build

**Option 1: Replace assets before build**

1. Replace `Assets/logo.png` with client's logo (recommended: PNG with transparency, ~120-200px wide)
2. Replace `Assets/app.ico` with client's icon (multi-resolution .ico file)
3. Update `Assets/config.default.json`:
   ```json
   "branding": {
     "productName": "Client IT Portal",
     "companyName": "Client Name",
     "primaryColor": "#client-color",
     "logo": "Assets/logo.png",
     "logoHeight": 40
   }
   ```
4. Build and deploy

**Option 2: External config file**

1. Deploy the standard build
2. Create `C:\ProgramData\InformationBox\config.json` with client-specific settings
3. Place client logo/icon files in a known location
4. Reference them in the config:
   ```json
   "branding": {
     "logo": "C:\\Company\\Branding\\logo.png",
     "icon": "C:\\Company\\Branding\\app.ico"
   }
   ```

**Option 3: Tenant overrides**

For multi-tenant deployments where different clients share the same installation:

```json
"tenantOverrides": {
  "client-a-tenant-id": {
    "branding": {
      "productName": "Client A Portal",
      "primaryColor": "#aa0000",
      "logo": "https://clienta.com/logo.png"
    }
  },
  "client-b-tenant-id": {
    "branding": {
      "productName": "Client B Portal",
      "primaryColor": "#00aa00",
      "logo": "https://clientb.com/logo.png"
    }
  }
}
```

### Logo Guidelines

| Property | Recommendation                                          |
| -------- | ------------------------------------------------------- |
| Format   | PNG with transparency                                   |
| Width    | 120-200 pixels (auto-scales)                            |
| Height   | 32-48 pixels                                            |
| Config   | Set `logoWidth: 0` for auto-width, specify `logoHeight` |

### Icon Guidelines

| Property | Recommendation                        |
| -------- | ------------------------------------- |
| Format   | .ico (Windows icon)                   |
| Sizes    | Include 16x16, 32x32, 48x48, 256x256  |
| Tool     | Use IcoFX, GIMP, or online converters |

---

## Azure AD App Registration

The Graph client ID in your config must map to a public-client app registration that supports brokered Windows SSO.

1. In **Azure Portal** → **Azure Active Directory** → **App registrations**, create or open your app
2. Under **Authentication**:
   - Enable **Allow public client flows**
   - Add redirect URIs under "Mobile and desktop applications":
     - `ms-appx-web://microsoft.aad.brokerplugin/{clientId}`
     - `https://login.microsoftonline.com/common/oauth2/nativeclient`
3. Under **API permissions**:
   - Add **Microsoft Graph** → **Delegated** → **User.Read**
   - Grant admin consent

Without proper configuration, authentication falls back to LDAP.

---

## Development

### Project Structure

```
InformationBox/
├── Assets/                 # Branding assets and default config
├── Config/                 # Configuration models
├── Services/               # Business logic (Graph, LDAP, etc.)
├── Themes/                 # XAML theme dictionaries
├── UI/
│   ├── Commands/           # ICommand implementations
│   └── ViewModels/         # MVVM view models
├── App.xaml                # Application entry
└── MainWindow.xaml         # Main window UI
```

### Adding a New Theme

1. Create `Themes/YourTheme.xaml` based on an existing theme
2. Add the theme name to `ThemeManager.AvailableThemes`
3. Define all required resource keys (see existing themes for reference)

### Build Targets

- Framework: `net10.0-windows`
- WPF with AOT-friendly patterns
- Warnings as errors, XML documentation enabled

---

## License

Copyright Evotec. All rights reserved.
