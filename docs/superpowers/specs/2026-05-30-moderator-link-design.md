# Moderator Link Selection in Room Picker — Design Spec

**Date:** 2026-05-30  
**Status:** Approved

## Overview

Add a second "Als Moderator einfügen" button to `RoomPickerForm`. When a room with a moderator access code is selected, the button becomes visible. Clicking it inserts the room's join URL with the moderator access code pre-filled as a query parameter (`?viewerCode=XXXX`), allowing the meeting organizer to join directly as moderator without manually entering the code.

---

## URL Format

**Standard (participant) link** — unchanged:
```
{GreenlightUrl}/rooms/{friendly_id}
```

**Moderator link** — new:
```
{GreenlightUrl}/rooms/{friendly_id}?viewerCode={moderatorCode}
```

Greenlight v3's `JoinCard.jsx` reads `viewerCode` from the URL query string and pre-fills the access code field. The server validates it against `glModeratorAccessCode` and assigns the `Moderator` BBB role.

---

## API Flow

**On room selection (proactive fetch):**

```
GET /api/v1/room_settings/{friendly_id}.json
→ { "glModeratorAccessCode": "xk92ab", "glViewerAccessCode": "", ... }
```

- `glModeratorAccessCode` non-empty → cache code in `_moderatorCode`, show `buttonInsertAsModerator`
- `glModeratorAccessCode` empty → hide button
- Fetch fails → hide button, log silently

**New `GreenlightClient` method:**

```csharp
Task<Dictionary<string, string>> GetRoomSettingsAsync(string friendlyId);
// GET /api/v1/room_settings/{friendlyId}.json
```

Follows the existing `BuildRequest` pattern (session cookie, same `HttpClient`).

---

## `RoomPickerForm` Changes

### Layout

```
┌─────────────────────────────────────────────────┐
│  BigBlueButton-Raum auswählen                   │
├─────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────┐    │
│  │ Mein Wochenmetting                      │    │
│  │ Team-Standup                      ← sel │    │
│  │ Projektbesprechung                      │    │
│  └─────────────────────────────────────────┘    │
│  [Status-Label]                                 │
│  [Neuen Raum anlegen]  [Abbrechen]  [Als Mod.]  │
│                                      [Einfügen] │
└─────────────────────────────────────────────────┘
```

`buttonInsertAsModerator` sits right of `buttonCancel`, left of `buttonInsert`. Default: `Visible = false`.

### State Machine on Selection Change

| Event | Buttons | Status Label |
|---|---|---|
| Room clicked | Insert disabled, AsMod hidden | `RoomPicker_CheckingModeratorCode` |
| Settings loaded, code present | Insert enabled, AsMod visible | empty |
| Settings loaded, no code | Insert enabled, AsMod hidden | empty |
| Settings fetch fails | Insert enabled, AsMod hidden | empty (silent) |

### Race Condition Handling

Each `ListRooms_SelectedIndexChanged` cancels the in-flight settings fetch via `CancellationToken` and starts a new one. The result is always consistent with the last selected room.

### Moderator Insert Handler

```csharp
private void ButtonInsertAsModerator_Click(object sender, EventArgs e)
{
    SelectedRoom = listRooms.SelectedItem as Room;
    // GreenlightUrl is accessed via ThisAddIn.Instance.Settings.GreenlightUrl (same pattern as TryLoginAsync)
    SelectedRoom.JoinUrl = new Uri(ThisAddIn.Instance.Settings.GreenlightUrl, $"rooms/{SelectedRoom.FriendlyId}?viewerCode={_moderatorCode}").ToString();
    DialogResult = DialogResult.OK;
    Close();
}
```

`RibbonHandler` and `AppointmentWriter` remain unchanged — they read `picker.SelectedRoom.JoinUrl` as before.

---

## Error Handling

| Situation | Behaviour |
|---|---|
| `GetRoomSettingsAsync` throws | Button hidden, `DebugLog.Write`, no user-visible error |
| `glModeratorAccessCode` is empty string | Button hidden |
| Session expired during settings fetch | `UnauthorizedAccessException` → login flow, retry fetch |
| Selection changes during in-flight fetch | `CancellationToken` cancels old fetch; new fetch starts |
| Room created via `CreateRoomForm` | No settings fetch; standard URL inserted directly |

---

## Localization

New strings added to `Strings.resx` (EN) and `Strings.de.resx` (DE):

| Key | DE | EN |
|---|---|---|
| `RoomPicker_InsertAsModeratorButton` | Als Moderator einfügen | Insert as Moderator |
| `RoomPicker_CheckingModeratorCode` | Moderator-Code wird geprüft… | Checking moderator code… |

---

## Out of Scope

- Embedding the viewer access code in the URL
- Moderator button in the `CreateRoomForm` flow
- Displaying the moderator code value in the picker UI
