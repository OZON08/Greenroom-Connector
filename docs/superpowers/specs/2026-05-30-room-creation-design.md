# Room Creation via Greenroom Connector — Design Spec

**Date:** 2026-05-30  
**Status:** Approved

## Overview

Add a "Neuen Raum anlegen" button to the bottom-left of `RoomPickerForm`. Clicking it opens a separate `CreateRoomForm` dialog where the user enters a room name and configures available settings. On "Anlegen & Einfügen" the room is created, settings are applied, and it is immediately inserted into the Outlook appointment — without going back to the picker list.

---

## API Flow

### On dialog open (once)

```
GET /api/v1/rooms_configurations.json
→ { "glRequireAuthentication": "optional",
    "record": "default_enabled",
    "glViewerAccessCode": "true",
    "guestPolicy": "optional", ... }
```

A setting is user-configurable if its config value is `"optional"` or `"default_enabled"`.  
Settings with `"true"` or `"false"` (admin-forced) are hidden.  
**Exception:** Access codes (`glViewerAccessCode`, `glModeratorAccessCode`) with config value `"true"` can still be toggled on/off per Greenlight API rules.

### On "Anlegen & Einfügen"

1. `POST /api/v1/rooms.json` → `{ room: { name: "..." } }`  
   Response body is the room path (`"/rooms/abc-def-ghi"`); extract `friendly_id` from it.

2. For each setting the user changed from its default:  
   `PATCH /api/v1/room_settings/{friendly_id}.json`  
   → `{ room_setting: { settingName: "record", settingValue: "true" } }`  
   One request per setting, run sequentially.

3. Build a `Room` object from `friendly_id` + name, construct `JoinUrl` using `GreenlightUrl` from `SettingsProvider`, return it to `RoomPickerForm`.

**Access codes:** Greenlight generates the code server-side. Send `"true"` to activate, `"false"` to deactivate.

---

## New `GreenlightClient` Methods

```csharp
Task<Dictionary<string, string>> GetRoomsConfigurationsAsync();
// GET /api/v1/rooms_configurations.json

Task<string> CreateRoomAsync(string name);
// POST /api/v1/rooms.json → returns friendly_id

Task UpdateRoomSettingAsync(string friendlyId, string settingName, string settingValue);
// PATCH /api/v1/room_settings/{friendlyId}.json
```

All three follow the existing `BuildRequest` pattern (session cookie, same `HttpClient`).

---

## New Model: `RoomConfiguration`

```csharp
public class RoomConfiguration
{
    private readonly Dictionary<string, string> _config;

    public RoomConfiguration(Dictionary<string, string> config) { _config = config; }

    // Returns true if the user may change this setting
    public bool CanChange(string settingName)
    {
        _config.TryGetValue(settingName, out var v);
        return v == "optional" || v == "default_enabled";
    }

    // Returns true if the setting is enabled by default
    public bool IsDefaultEnabled(string settingName)
    {
        _config.TryGetValue(settingName, out var v);
        return v == "default_enabled";
    }

    // Access codes: admin forced-on but still user-toggleable
    public bool CanToggleAccessCode(string settingName)
    {
        _config.TryGetValue(settingName, out var v);
        return CanChange(settingName) || v == "true";
    }
}
```

---

## New UI: `CreateRoomForm`

A standalone WinForms dialog, consistent with the existing `LoginWindow` / `RoomPickerForm` style.

### Layout

```
┌─────────────────────────────────────────┐
│  Neuen Raum anlegen                     │
├─────────────────────────────────────────┤
│  Name                                   │
│  [ Raum-Name eingeben...              ] │
│                                         │
│  Einstellungen                          │
│  ┌───────────────────────────────────┐  │
│  │ ☐ Aufnahme erlauben               │  │
│  │ ☐ Authentifizierung erforderlich  │  │
│  │ ☐ Alle als Moderator              │  │
│  │ ☐ Zugangscode für Zuschauer       │  │
│  │ ☐ Zugangscode für Moderatoren     │  │
│  │                                   │  │
│  │ Gäste: ○ Immer erlauben           │  │
│  │        ○ Moderator fragen         │  │
│  │        ○ Immer ablehnen           │  │
│  └───────────────────────────────────┘  │
│                                         │
│  [Status-/Fehlermeldung]                │
│                          [Abbrechen]    │
│                   [Anlegen & Einfügen]  │
└─────────────────────────────────────────┘
```

### Behaviour

- On load: `GET /api/v1/rooms_configurations.json`. Show "Einstellungen werden geladen…" + busy cursor.
- Each control is shown only if `RoomConfiguration.CanChange()` (or `CanToggleAccessCode()` for codes) returns true.
- Controls for settings with `"default_enabled"` are pre-checked; all others start unchecked.
- If config fetch fails: show all settings as fallback, display `CreateRoom_ConfigError` in status label.
- "Anlegen & Einfügen" is disabled while name is empty or loading is in progress.
- On submit error (POST fails): show error in status label, dialog stays open.
- On session expiry: trigger login flow (same pattern as `RoomPickerForm.TryLoginAsync`), then retry POST.

### Result

`CreateRoomForm.CreatedRoom` — a fully populated `Room` object (id, name, friendly_id, JoinUrl).

---

## `RoomPickerForm` Changes

- Add `buttonNewRoom` (bottom-left, `Anchor = Left | Bottom`, label = `Strings.RoomPicker_NewRoomButton`).
- Click handler:

```csharp
private void ButtonNewRoom_Click(object sender, EventArgs e)
{
    using (var form = new CreateRoomForm())
    {
        if (form.ShowDialog(this) != DialogResult.OK || form.CreatedRoom == null)
            return;
        SelectedRoom = form.CreatedRoom;
        DialogResult = DialogResult.OK;
        Close();
    }
}
```

- The new room does **not** appear in the list — the picker closes immediately with the created room.

---

## Error Handling Summary

| Situation | Behaviour |
|---|---|
| Config load fails | Fallback: show all settings; status label shows config error |
| POST fails (name conflict, room limit) | Error in status label, dialog stays open |
| POST fails (session expired) | Login flow, then retry |
| Individual PATCH fails | `DebugLog.Write`, continue remaining PATCHes |
| All PATCHes fail | Room inserted with default settings (no blocking error) |

---

## Localization

New strings added to `Strings.resx` (EN) and `Strings.de.resx` (DE):

| Key | DE | EN |
|---|---|---|
| `CreateRoom_Title` | Neuen Raum anlegen | Create New Room |
| `CreateRoom_NameLabel` | Name | Name |
| `CreateRoom_SettingsGroup` | Einstellungen | Settings |
| `CreateRoom_Record` | Aufnahme erlauben | Allow recording |
| `CreateRoom_RequireAuth` | Nur angemeldete Nutzer | Require authentication |
| `CreateRoom_AnyoneModerator` | Alle als Moderator | Anyone can moderate |
| `CreateRoom_ViewerCode` | Zugangscode für Zuschauer | Viewer access code |
| `CreateRoom_ModeratorCode` | Zugangscode für Moderatoren | Moderator access code |
| `CreateRoom_GuestPolicy` | Gäste | Guests |
| `CreateRoom_GuestAlways` | Immer erlauben | Always allow |
| `CreateRoom_GuestAsk` | Moderator fragen | Ask moderator |
| `CreateRoom_GuestDeny` | Immer ablehnen | Always deny |
| `CreateRoom_SubmitButton` | Anlegen & Einfügen | Create & Insert |
| `CreateRoom_LoadingConfig` | Einstellungen werden geladen… | Loading settings… |
| `CreateRoom_ConfigError` | Einstellungen konnten nicht geladen werden | Could not load settings |
| `RoomPicker_NewRoomButton` | Neuen Raum anlegen | New Room |

---

## Out of Scope

- Raum-Einstellungen nach der Erstellung bearbeiten
- Räume löschen
- Räume umbenennen
- Anzeige des generierten Zugangscodes im Formular (Greenlight generiert ihn serverseitig; der Nutzer sieht ihn erst in der Greenlight-Web-UI)
