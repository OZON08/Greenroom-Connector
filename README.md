<p align="center">
  <img src="docs/images/Greenroom%20Connector%20Logo.png" alt="Greenroom Connector" width="360" />
</p>

<h1 align="center">Greenroom Connector</h1>

<p align="center">
  Outlook add-in that drops a join link for your
  <a href="https://docs.bigbluebutton.org/greenlight/v3/install">Greenlight</a> /
  BigBlueButton room into any appointment.
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License" /></a>
  <img src="https://img.shields.io/badge/Outlook-2024%20%7C%20365%20Classic-0F6CBD" alt="Outlook 2024 / 365 Classic" />
  <img src="https://img.shields.io/badge/.NET-Framework%204.8-512BD4" alt=".NET Framework 4.8" />
</p>

---

## What it does

A button on the Outlook *Appointment* ribbon opens a small picker showing your
Greenlight rooms (fetched live from the server you authenticated against). Pick
one, hit **Insert**, and the appointment gets:

- a clearly-marked block in the body with a clickable join URL
- the configured location text (e.g. *„BigBlueButton-Konferenz"*) in the
  *Location* field — only when the user hasn't typed something else there
- optionally an admin-controlled phone dial-in section, if your deployment
  uses one

Works for the meeting organiser **and** for every recipient of the invitation
— the link is part of the appointment body, so no add-in install is needed on
the attendee side.

## How it looks

Ribbon button on a new appointment, room picker, and the resulting body block.

> _Screenshots will be added once the first signed build is out._

## Features

- **Live Greenlight room list** — pulls `/api/v1/rooms.json` for the signed-in
  user; no manual room copy-paste, no admin pre-config
- **Keycloak / OIDC SSO** — the embedded Microsoft Edge WebView2 hosts the
  real Greenlight sign-in flow, so any auth method your IdP supports works
  (password, MFA, conditional access)
- **Per-machine silent install** — WiX v4 MSI with public properties; deploy
  via Group Policy / Intune without user interaction
- **Localised UI** — German and English ship in the box; add more by dropping
  a new `Strings.<culture>.resx` next to the existing ones
- **Brand-correct visuals** — Microsoft 365 Fluent button styling, brand
  blue accent, multi-size app icon (16/24/32/48/64/128 px)
- **Dial-in section** is admin-controlled: text in resx (translatable),
  phone number per deployment in registry, on/off toggle in registry

## Quick start (administrator)

> The signed MSI build is not yet automated — see
> [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) for the local build instructions.

Once the MSI is available, deploy silently with:

```powershell
msiexec /i GreenroomConnector.msi /qn `
    GREENLIGHTURL=https://your-greenlight.example.org `
    LANGUAGE=auto `
    LOCATIONTEXT="BigBlueButton-Konferenz" `
    SHOWDIALIN=true `
    DIALINNUMBER="+49 30 1234 5678"
```

## Configuration

All admin settings live under
`HKEY_LOCAL_MACHINE\SOFTWARE\GreenroomConnector` (REG_SZ) and are written by
the MSI. Per-user state (the cached Keycloak session cookie, DPAPI-encrypted)
lives under `HKEY_CURRENT_USER\Software\GreenroomConnector`.

| Value          | Purpose | Example |
|----------------|---------|---------|
| `GreenlightUrl` | Base URL of your Greenlight instance | `https://meet.example.org` |
| `Language`      | UI culture (`auto`, `de`, `en`) | `auto` |
| `LocationText`  | Template for the `Location` field. `{room}` is replaced with the room name. Empty = leave Location untouched. | `BigBlueButton: {room}` |
| `ShowDialIn`    | `true` / `false` toggle for the dial-in section | `true` |
| `DialInNumber`  | Phone number substituted into `Strings.Meeting_DialIn` via `{number}` | `+49 30 1234 5678` |

The wrapping text around the dial-in number is translatable and lives in
[`src/GreenroomConnector/Resources/Strings.resx`](src/GreenroomConnector/Resources/Strings.resx)
(EN) and [`Strings.de.resx`](src/GreenroomConnector/Resources/Strings.de.resx) (DE).

## Limitations

- **Outlook reminder „Join" button**: Microsoft reserves the native join
  button in the reminder pop-up of *Classic Outlook on Windows* for
  certified providers (Teams, Zoom, …). Custom Outlook add-ins cannot
  surface there. The link is still in the appointment body, so users open
  the appointment from the reminder ("Open Item") and click the link.
  The native button does work in Outlook Web / new Outlook / Mac / mobile
  with a separate Office Web Add-in — see *Roadmap* below.
- **HTML body**: in Office 2024 / 365 classic the live `AppointmentItem`
  COM surface refuses `HTMLBody` writes via late binding, so the join
  block is inserted as plain text. Outlook auto-detects URLs and turns
  them into clickable links, so the user experience is identical.

## Roadmap

- Signed MSI build via GitHub Actions
- Companion Outlook Web Add-in (registers as an Online-Meeting provider so
  the native reminder Join button lights up in Outlook Web / new Outlook /
  mobile)
- Optional WPF / Fluent rewrite of the picker for a more native Win11 feel

## Development

Build and run from source — see [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md)
for the full setup (Office workload requirement, dev registry script, optional
local Greenlight + Keycloak Docker stack for offline work).

## Acknowledgements

Built on top of the wonderful [BigBlueButton](https://bigbluebutton.org/) and
[Greenlight v3](https://github.com/bigbluebutton/greenlight) projects.
This add-in is a **client** of the Greenlight HTTP API; no Greenlight code is
bundled.

## License

[MIT](LICENSE) — © 2026 OZON08

## Support the project

If this add-in saves your team a couple of clicks per meeting and you want to
say thanks, you can sponsor the project via
[GitHub Sponsors](https://github.com/sponsors/OZON08). All sponsorships go
straight to keeping the lights on for the integration work.
