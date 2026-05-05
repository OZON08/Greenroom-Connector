# Development setup

How to build, debug and test **Greenroom Connector** locally — either against a
remote Greenlight server you already have an account on, or against the
self-contained Docker stack shipped under [`compose/`](../compose).

## Prerequisites

- Windows 10 / 11 with **Outlook 2024 Classic** or **Office 365 Classic** (not
  the rewritten "new Outlook")
- **Visual Studio 2022** with the **Office/SharePoint development** workload
- **Microsoft Edge WebView2 Runtime** (pre-installed on current Windows 10/11)
- Administrator access — the add-in reads its config from `HKLM`

Verify the workload and the WebView2 runtime:

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

Either of:

- a **remote Greenlight** instance you already use (pass `-GreenlightUrl https://…`
  to the dev-registry script below), or
- the **local Greenlight + Keycloak** Docker stack — useful for offline work,
  testing breaking changes, or when you don't have an account yet. See
  [Local stack via Docker Compose](#local-stack-via-docker-compose).

## 2. Write the dev registry config

The add-in reads its config from `HKLM\SOFTWARE\GreenroomConnector`. The MSI
writes these at install time; for a dev build run the script manually from an
**elevated** PowerShell:

```powershell
# Default: localhost:3000, German "BigBlueButton-Konferenz" as Location text,
# dial-in disabled, sample phone number.
.\scripts\Set-DevRegistry.ps1

# Other server, English UI:
.\scripts\Set-DevRegistry.ps1 -GreenlightUrl https://meet.example.org -Language en

# Enable the dial-in section with a real number:
.\scripts\Set-DevRegistry.ps1 -ShowDialIn -DialInNumber "+49 30 9876 5432"

# Custom Location-field template (use {room} as a placeholder for the room name):
.\scripts\Set-DevRegistry.ps1 -LocationText "BigBlueButton: {room}"

# Tear it down:
.\scripts\Set-DevRegistry.ps1 -Remove
```

The full set of supported registry values is documented in the project
[README](../README.md#configuration).

## 3. Build and debug

```powershell
start .\GreenroomConnector.sln
```

In Visual Studio:

1. Right-click the solution → **Restore NuGet Packages**
2. Right-click **GreenroomConnector** → **Set as Startup Project**
3. **F5**

VSTO registers the add-in under
`HKCU\Software\Microsoft\Office\Outlook\Addins\GreenroomConnector` for the
debug session and unregisters it when VS closes — no manual setup needed for
the F5 loop.

If Outlook shows a *"publisher not verified"* warning, click **Install**. The
binaries are unsigned during dev — see [Release & code signing](#release--code-signing)
for what production builds need.

## 4. First test

1. In Outlook, create a **new appointment**
2. On the *Termin* / *Appointment* ribbon you should see a group
   **BigBlueButton** with the button **BigBlueButton-Meeting einfügen** /
   **Add BigBlueButton meeting**
3. Click the button — a WebView2 window opens with the Keycloak sign-in (the
   Greenlight SPA auto-redirects via `?sso=true`)
4. Sign in → the window closes on its own as soon as `/api/v1/rooms.json`
   returns 200
5. The Greenroom Connector room picker opens with your rooms
6. Select a room → **Einfügen** / **Insert**
7. The appointment body now contains a clearly delimited block with a
   clickable join link (and, if enabled, the dial-in section). The *Location*
   field is filled with the configured `LocationText` (e.g.
   *BigBlueButton-Konferenz*) — but **only** when the field was empty.

## 5. Smoke checklist

| Observation | Verifies |
|---|---|
| WebView auto-jumps to Keycloak without a Greenlight login page in between | `?sso=true` triggers the OmniAuth form submit on the SPA |
| The login window closes itself after Keycloak auth | The `/api/v1/rooms.json` polling inside the WebView detects login |
| The picker lists *your* rooms (not empty, not someone else's) | Cookie propagation + serializer shape match |
| Clicking the inserted link lands you in the right room | Join URL pattern `/rooms/<friendly_id>` is correct |
| `Location` shows your `LocationText`, not the URL | `LocationText` is read from HKLM and applied |
| Dial-in section appears (or doesn't) per `ShowDialIn` flag | Toggle plumbing works, `{number}` substitution works |

If anything misfires, enable the verbose file logger by running

```powershell
.\scripts\Set-DevRegistry.ps1 -DebugLogging
```

(or set `HKLM\SOFTWARE\GreenroomConnector\DebugLogging` to `true` directly).
Restart Outlook, reproduce the issue and inspect
`%LOCALAPPDATA%\GreenroomConnector\debug.log`. The log contains full HTTP
responses to `/api/v1/rooms.json` (status + body) plus any exception in
`AppointmentWriter` with full stack trace. The file rotates to `debug.log.old`
once it exceeds 1 MB. **Disable again** (`-DebugLogging:$false`) when you are
done — the file holds room IDs, owners and friendly IDs, which are all
join-link material.

## Local stack via Docker Compose

For offline work or breaking-change testing,
[`compose/docker-compose.dev.yml`](../compose/docker-compose.dev.yml) brings up
Greenlight v3, Postgres, Redis and a pre-configured Keycloak realm.

**Bring the stack up:**

```powershell
docker compose -f compose/docker-compose.dev.yml up -d
docker compose -f compose/docker-compose.dev.yml logs -f greenlight   # wait for migrations + boot
```

First boot takes ~30–60 s while Greenlight runs migrations.

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
[`compose/keycloak/greenlight-realm.json`](../compose/keycloak/greenlight-realm.json)):

- `test@example.com` / `test123`
- `alice@example.com` / `alice123`

**How OIDC works without a hosts-file edit:** Keycloak signs tokens with
issuer `http://localhost:8080/...`, which the browser resolves natively.
Greenlight (in the container) reaches Keycloak via the Docker DNS name
`keycloak`. A mounted
[`compose/greenlight/omniauth.rb`](../compose/greenlight/omniauth.rb) override
splits the OIDC config so the authorization endpoint uses `localhost` while
token, userinfo and JWKS calls go through the bridge network. The token
`iss` claim still equals the configured issuer, so validation passes.

**Caveats:**

- No real BBB server is included. Listing rooms works; actually starting a
  meeting will fail (the join URL points at `localhost:3000/rooms/<id>`
  which BBB can't reach). That's fine for testing the add-in.
- A new Keycloak user is materialised in Greenlight's DB on first sign-in.
  They have no rooms by default — create one in the Greenlight UI before
  testing the picker.
- If you ever hit the stack while it was still redirecting HTTP → HTTPS
  (early-iteration bug), your browser cached the 301. Use a fresh incognito
  window or clear cache for `localhost:3000`.

**Wipe everything (volumes included):**

```powershell
docker compose -f compose/docker-compose.dev.yml down -v
```

## Translations

UI strings live in
[`src/GreenroomConnector/Resources/Strings.resx`](../src/GreenroomConnector/Resources/Strings.resx)
(English, neutral) and `Strings.de.resx` (German). To add another language,
copy `Strings.resx` to `Strings.<culture>.resx` (e.g. `Strings.fr.resx`) and
translate the values. The add-in picks the closest match to the Outlook UI
culture at startup; you can also pin it via the `Language` registry value.

The `Strings.Meeting_DialIn` resource contains the wrapping text around the
dial-in number; `{number}` is substituted at runtime with the
`DialInNumber` registry value.

## Release & code signing

The dev build is signed with a self-issued, gitignored test key
(`GreenroomConnector_TemporaryKey.pfx`, thumbprint `7C1CF8…`). That is enough
to load the add-in locally but **not** for distribution:

- **VSTO manifests** (`.vsto`, `.dll.manifest`) need to be signed with a
  certificate that chains to a CA Outlook trusts; otherwise users see a
  *"publisher not verified"* prompt on first load (or, with stricter Outlook
  trust settings, the add-in is silently disabled).
- **Add-in assembly** (`GreenroomConnector.dll`) should carry an Authenticode
  signature for SmartScreen and corporate AV reputation.
- **MSI** (`GreenroomConnector.msi`) should be Authenticode-signed too —
  otherwise SmartScreen warns at install time and Group-Policy-managed
  endpoints may block it outright.

Production checklist before tagging a release:

1. Replace `<ManifestKeyFile>` / `<ManifestCertificateThumbprint>` in
   [`GreenroomConnector.csproj`](../src/GreenroomConnector/GreenroomConnector.csproj)
   with the production code-signing certificate (kept out of the repo —
   load it from the Windows certificate store via thumbprint, or feed it in
   from CI secrets).
2. Sign the built `GreenroomConnector.dll` with `signtool sign /fd SHA256
   /tr http://timestamp.digicert.com /td SHA256 …` (use any RFC 3161
   timestamp server) before packaging.
3. Sign `GreenroomConnector.msi` after WiX builds it, with the same cert
   and timestamp options.
4. Verify the chain on a clean VM: `signtool verify /pa /v
   GreenroomConnector.msi`.

The temp key may stay in the csproj for the F5 dev loop; just don't ship the
artifacts it produces.

## Silent install (once the MSI is built)

```cmd
msiexec /i GreenroomConnector.msi /qn ^
    GREENLIGHTURL=https://meet.example.org ^
    LANGUAGE=auto ^
    LOCATIONTEXT="BigBlueButton-Konferenz" ^
    SHOWDIALIN=true ^
    DIALINNUMBER="+49 30 1234 5678" ^
    DEBUGLOGGING=false
```

`GREENLIGHTURL` is required and must use `https://` unless it points at a
loopback host (`localhost`, `127.0.0.1`, `::1`) — the add-in rejects plain
HTTP for any other host because the session cookie would otherwise travel
unencrypted. The remaining properties are optional with sensible defaults.
