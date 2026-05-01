# Development setup

How to build, debug and test the Outlook add-in locally against a real Greenlight
server (e.g. `http://localhost:3000`).

## Prerequisites

- Windows 10/11 with **Outlook 2024 Classic** (not "new Outlook")
- **Visual Studio 2022** with the **Office/SharePoint development** workload
- **Microsoft Edge WebView2 Runtime** (pre-installed on current Windows 10/11)
- Administrator access (the add-in reads config from `HKLM`)

Verify the VS workload and WebView2 runtime:

```powershell
# Must list "Microsoft.VisualStudio.Workload.Office"
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.VisualStudio.Workload.Office -format json |
    ConvertFrom-Json | Select-Object displayName, installationPath

# Must return a version (pv)
Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}' `
    -ErrorAction SilentlyContinue | Select-Object pv
```

If the Office workload is missing: Visual Studio Installer → *Modify* → tick
**Office/SharePoint development**.

## 1. Pick a Greenlight target

You can develop against either:

- a **remote Greenlight** (e.g. `http://localhost:3000`) — fastest if you
  already have an account there, or
- a **local Greenlight + Keycloak stack** via Docker — useful for offline
  work, breaking changes, or testing edge cases. See
  [Local stack via Docker Compose](#local-stack-via-docker-compose) below.

## 2. Write the dev registry config

The add-in reads its server URL and language from `HKLM`. At install time the
MSI writes these; for local development run the script manually from an
**elevated** PowerShell:

```powershell
.\scripts\Set-DevRegistry.ps1                                        # default: meet.wald.rlp.de
.\scripts\Set-DevRegistry.ps1 -GreenlightUrl http://localhost:3000   # local stack
.\scripts\Set-DevRegistry.ps1 -GreenlightUrl https://other.example.com -Language de
```

To tear it down later:

```powershell
.\scripts\Set-DevRegistry.ps1 -Remove
```

## 3. Build and debug

```powershell
start .\GreenroomConnector.sln
```

In Visual Studio:

1. Right-click the solution → **Restore NuGet Packages**
2. Right-click **GreenroomConnector** → **Set as Startup Project**
3. **F5**

VSTO registers the add-in automatically under
`HKCU\Software\Microsoft\Office\Outlook\Addins\GreenroomConnector` while
debugging and unregisters it when VS closes. No manual add-in registration is
needed for the F5 loop.

If Outlook shows a "publisher not verified" warning, click **Install** — the
binaries are currently unsigned. Code signing will be added later.

## 4. First test

1. In Outlook, create a **new appointment**
2. On the *Appointment* ribbon you should see a group **Greenlight** with the
   button **Add Greenlight meeting** / **Greenlight-Meeting einfügen**
3. Click the button
4. A WebView2 window opens and auto-redirects to Keycloak
5. Sign in → the window closes on its own
6. The room picker opens with your rooms
7. Select a room → **Insert**
8. The appointment body now contains an "Online beitreten" / "Join online"
   link, and the *Location* field is filled with the join URL

## 5. What to verify (smoke checklist)

| Observation | Verifies |
|---|---|
| WebView2 auto-jumps to Keycloak without a Greenlight login page in between | `?sso=true` triggers the OmniAuth form submit |
| The login window closes itself after successful Keycloak auth | The `/api/v1/rooms.json` polling inside the WebView detects login |
| The room picker lists *your* rooms (not empty, not someone else's) | Cookie propagation + serializer shape match |
| Clicking the inserted "Online beitreten" link lands you in the room | Join URL pattern `/rooms/<friendly_id>` is correct |

If anything misfires, grab the stack trace from VS *Debug → Output* or from
Event Viewer → *Windows Logs → Application* and file it with the failing step.

## Local stack via Docker Compose

For offline work or to test breaking changes, [compose/docker-compose.dev.yml](../compose/docker-compose.dev.yml)
brings up Greenlight v3, Postgres, Redis and a pre-configured Keycloak realm.

**Bring the stack up:**

```powershell
docker compose -f compose/docker-compose.dev.yml up -d
docker compose -f compose/docker-compose.dev.yml logs -f greenlight   # wait for migrations + boot
```

First boot takes ~30–60s while Greenlight runs migrations.

**Point the add-in at the local stack:**

```powershell
.\scripts\Set-DevRegistry.ps1 -GreenlightUrl http://localhost:3000
```

**Endpoints:**

| What           | URL                          | Credentials            |
| -------------- | ---------------------------- | ---------------------- |
| Greenlight     | <http://localhost:3000>      | login via Keycloak SSO |
| Keycloak admin | <http://localhost:8080>      | `admin` / `admin`      |
| Keycloak realm | `greenlight` (auto-imported) | —                      |

Pre-seeded test users (defined in
[compose/keycloak/greenlight-realm.json](../compose/keycloak/greenlight-realm.json)):

- `test@example.com` / `test123`
- `alice@example.com` / `alice123`

**How OIDC works without a hosts-file edit:** Keycloak signs tokens with
issuer `http://localhost:8080/...`, which the browser resolves natively.
Greenlight (in the container) reaches Keycloak via the Docker DNS name
`keycloak`. A mounted [omniauth.rb override](../compose/greenlight/omniauth.rb)
splits the OIDC config so the authorization endpoint uses `localhost` while
token, userinfo and JWKS calls go through the bridge network. The token
`iss` claim still equals the configured issuer, so validation passes.

**Caveats:**

- No real BBB server. Listing rooms works; actually starting a meeting will
  fail (the join URL points at `localhost:3000/rooms/<id>` which BBB can't
  reach). That is fine for testing the add-in.
- Initial Greenlight signup via Keycloak creates the user in Greenlight's DB
  on first login. They get no rooms by default — create one in the Greenlight
  UI before testing the room picker.
- If you previously hit the stack while it was redirecting HTTP→HTTPS, your
  browser cached the 301 redirect. Use a fresh incognito window or clear
  the cache for `localhost:3000`.

**Wipe everything (volumes included):**

```powershell
docker compose -f compose/docker-compose.dev.yml down -v
```

## Known unknowns (until first real login)

These assumptions are baked into the code but can only be confirmed against a
live, authenticated session:

- **`OMNIAUTH_PATH`**: the Greenlight SPA reads it from a build-time env var;
  stock deployments use `/auth/openid_connect`. If `?sso=true` does not
  redirect to Keycloak, that path differs on your instance.
- **Extended-session cookie** (`_extended_session`): Greenlight's "remember me".
  If Keycloak sets it, we may need to capture it alongside the main session
  cookie in [LoginWindow.cs](../src/GreenroomConnector/UI/LoginWindow.cs).
- **Real response shape of `/api/v1/rooms.json`**: the parser expects
  `{"data":[...], "meta":{}}` with fields `id`, `name`, `friendly_id`,
  `online`, `participants`, `last_session`, optional `shared_owner`.

## Silent install (MSI, once the installer project builds)

```cmd
msiexec /i GreenroomConnector.msi /qn ^
    GREENLIGHTURL=http://localhost:3000 ^
    LANGUAGE=auto
```

`GREENLIGHTURL` is required; `LANGUAGE` defaults to `auto` (= Outlook UI
language, fallback English).
