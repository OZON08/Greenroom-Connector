# Room Creation & Moderator Link — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Neuen Raum anlegen" button to `RoomPickerForm` that opens a `CreateRoomForm` for creating rooms with adaptive settings, and add an "Als Moderator einfügen" button that appears when the selected room has a moderator access code.

**Architecture:** `GreenlightClient` gains four new async methods covering the `rooms_configurations`, `rooms` (POST), `room_settings` (PATCH), and `room_settings` (GET) endpoints. A new `RoomConfiguration` model wraps the configuration dictionary and decides which controls are shown. `CreateRoomForm` is a standalone dialog that creates the room, applies settings sequentially, and returns the new `Room` to `RoomPickerForm`, which either directly closes (new room path) or shows the second button when a moderator code exists on the selected room.

**Tech Stack:** C# / .NET Framework 4.8, WinForms, Newtonsoft.Json, xUnit 2.x

**Specs:** `docs/superpowers/specs/2026-05-30-room-creation-design.md` and `docs/superpowers/specs/2026-05-30-moderator-link-design.md`

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `src/GreenroomConnector/Models/RoomConfiguration.cs` | Wraps config dict, decides control visibility |
| Create | `src/GreenroomConnector/UI/CreateRoomForm.cs` | Room creation dialog logic |
| Create | `src/GreenroomConnector/UI/CreateRoomForm.Designer.cs` | WinForms layout for CreateRoomForm |
| Modify | `src/GreenroomConnector/Services/GreenlightClient.cs` | 4 new API methods + 2 static parse helpers |
| Modify | `src/GreenroomConnector/UI/RoomPickerForm.cs` | New Room + As Moderator handlers |
| Modify | `src/GreenroomConnector/UI/RoomPickerForm.Designer.cs` | 2 new buttons, wider form |
| Modify | `src/GreenroomConnector/Resources/Strings.resx` | English strings |
| Modify | `src/GreenroomConnector/Resources/Strings.de.resx` | German strings |
| Create | `tests/GreenroomConnector.Tests/RoomConfigurationTests.cs` | Unit tests for RoomConfiguration |
| Modify | `tests/GreenroomConnector.Tests/RoomParsingTests.cs` | Tests for new parse helpers |

**Note:** This is an old-style VSTO `.csproj`. After creating new `.cs` files, right-click the project in Visual Studio → **Add → Existing Item** to include them, or manually add `<Compile Include="..."/>` entries to the `.csproj`.

---

## Task 1: `RoomConfiguration` Model

**Files:**
- Create: `src/GreenroomConnector/Models/RoomConfiguration.cs`
- Create: `tests/GreenroomConnector.Tests/RoomConfigurationTests.cs`

- [ ] **Step 1.1 — Write failing tests**

Create `tests/GreenroomConnector.Tests/RoomConfigurationTests.cs`:

```csharp
using System.Collections.Generic;
using GreenroomConnector.Models;
using Xunit;

namespace GreenroomConnector.Tests
{
    public class RoomConfigurationTests
    {
        private static RoomConfiguration Config(string key, string value) =>
            new RoomConfiguration(new Dictionary<string, string> { { key, value } });

        private static RoomConfiguration Empty() =>
            new RoomConfiguration(new Dictionary<string, string>());

        [Theory]
        [InlineData("optional", true)]
        [InlineData("default_enabled", true)]
        [InlineData("true", false)]
        [InlineData("false", false)]
        [InlineData("", false)]
        public void CanChange_returns_expected(string configValue, bool expected) =>
            Assert.Equal(expected, Config("s", configValue).CanChange("s"));

        [Fact]
        public void CanChange_returns_false_for_missing_key() =>
            Assert.False(Empty().CanChange("missing"));

        [Theory]
        [InlineData("default_enabled", true)]
        [InlineData("optional", false)]
        [InlineData("true", false)]
        [InlineData("false", false)]
        public void IsDefaultEnabled_returns_expected(string configValue, bool expected) =>
            Assert.Equal(expected, Config("s", configValue).IsDefaultEnabled("s"));

        [Theory]
        [InlineData("optional", true)]
        [InlineData("default_enabled", true)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("", false)]
        public void CanToggleAccessCode_returns_expected(string configValue, bool expected) =>
            Assert.Equal(expected, Config("glViewerAccessCode", configValue).CanToggleAccessCode("glViewerAccessCode"));
    }
}
```

- [ ] **Step 1.2 — Run tests to confirm they fail**

```
dotnet test tests\GreenroomConnector.Tests\ --filter "FullyQualifiedName~RoomConfigurationTests"
```

Expected: build error — `RoomConfiguration` not found.

- [ ] **Step 1.3 — Implement `RoomConfiguration`**

Create `src/GreenroomConnector/Models/RoomConfiguration.cs`:

```csharp
using System.Collections.Generic;

namespace GreenroomConnector.Models
{
    public class RoomConfiguration
    {
        private readonly Dictionary<string, string> _config;

        public RoomConfiguration(Dictionary<string, string> config)
        {
            _config = config ?? new Dictionary<string, string>();
        }

        public bool CanChange(string settingName)
        {
            _config.TryGetValue(settingName, out var v);
            return v == "optional" || v == "default_enabled";
        }

        public bool IsDefaultEnabled(string settingName)
        {
            _config.TryGetValue(settingName, out var v);
            return v == "default_enabled";
        }

        public bool CanToggleAccessCode(string settingName)
        {
            _config.TryGetValue(settingName, out var v);
            return v == "optional" || v == "default_enabled" || v == "true";
        }
    }
}
```

- [ ] **Step 1.4 — Run tests to confirm they pass**

```
dotnet test tests\GreenroomConnector.Tests\ --filter "FullyQualifiedName~RoomConfigurationTests"
```

Expected: all 14 tests pass.

- [ ] **Step 1.5 — Add files to project and commit**

Add both files to the Visual Studio project (right-click → Add Existing Item), then:

```
git add src/GreenroomConnector/Models/RoomConfiguration.cs tests/GreenroomConnector.Tests/RoomConfigurationTests.cs
git commit -m "Add RoomConfiguration model with unit tests"
```

---

## Task 2: `GreenlightClient` — Parse Helpers & Tests

**Files:**
- Modify: `src/GreenroomConnector/Services/GreenlightClient.cs`
- Modify: `tests/GreenroomConnector.Tests/RoomParsingTests.cs`

- [ ] **Step 2.1 — Write failing tests**

Append to `tests/GreenroomConnector.Tests/RoomParsingTests.cs` (inside the existing `RoomParsingTests` class, before the closing `}`):

```csharp
    // --- ParseConfigurations ---

    [Fact]
    public void ParseConfigurations_extracts_data_object()
    {
        var json = "{\"data\":{\"record\":\"optional\",\"glRequireAuthentication\":\"default_enabled\"}}";
        var result = GreenlightClient.ParseConfigurations(json);
        Assert.Equal("optional", result["record"]);
        Assert.Equal("default_enabled", result["glRequireAuthentication"]);
    }

    [Fact]
    public void ParseConfigurations_returns_empty_for_empty_data()
    {
        var result = GreenlightClient.ParseConfigurations("{\"data\":{}}");
        Assert.Empty(result);
    }

    // --- ExtractFriendlyId ---

    [Theory]
    [InlineData("{\"data\":\"/rooms/abc-def-ghi\"}", "abc-def-ghi")]
    [InlineData("{\"data\":\"/rooms/xyz-uvw-123\"}", "xyz-uvw-123")]
    public void ExtractFriendlyId_parses_room_path(string json, string expected) =>
        Assert.Equal(expected, GreenlightClient.ExtractFriendlyId(json));

    [Fact]
    public void ExtractFriendlyId_throws_for_malformed_path()
    {
        Assert.Throws<System.InvalidOperationException>(
            () => GreenlightClient.ExtractFriendlyId("{\"data\":\"notapath\"}"));
    }
```

Also add `using GreenroomConnector.Services;` to the top of `RoomParsingTests.cs` if not already present.

- [ ] **Step 2.2 — Run to confirm they fail**

```
dotnet test tests\GreenroomConnector.Tests\ --filter "FullyQualifiedName~ParseConfigurations|FullyQualifiedName~ExtractFriendlyId"
```

Expected: build error — methods not found.

- [ ] **Step 2.3 — Add parse helpers to `GreenlightClient`**

Add these two `internal static` methods to `GreenlightClient.cs`, just before the `private List<Room> ParseRooms` method:

```csharp
internal static Dictionary<string, string> ParseConfigurations(string json)
{
    var token = JToken.Parse(json);
    var data = (token is JObject obj ? obj["data"] : token) as JObject ?? new JObject();
    return data.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
}

internal static string ExtractFriendlyId(string json)
{
    var token = JToken.Parse(json);
    var path = (token is JObject obj ? obj["data"]?.ToString() : null) ?? string.Empty;
    // path = "/rooms/abc-def-ghi"
    var segments = path.Trim('/').Split('/');
    if (segments.Length < 2 || string.IsNullOrEmpty(segments[1]))
        throw new InvalidOperationException("Unexpected room creation response: " + path);
    return segments[1];
}
```

Add `using System.Collections.Generic;` at the top of `GreenlightClient.cs` if not already present.

- [ ] **Step 2.4 — Run tests to confirm they pass**

```
dotnet test tests\GreenroomConnector.Tests\ --filter "FullyQualifiedName~ParseConfigurations|FullyQualifiedName~ExtractFriendlyId"
```

Expected: all 4 tests pass.

- [ ] **Step 2.5 — Commit**

```
git add src/GreenroomConnector/Services/GreenlightClient.cs tests/GreenroomConnector.Tests/RoomParsingTests.cs
git commit -m "Add ParseConfigurations and ExtractFriendlyId helpers with tests"
```

---

## Task 3: `GreenlightClient` — Four New HTTP Methods

**Files:**
- Modify: `src/GreenroomConnector/Services/GreenlightClient.cs`

All four methods follow the existing `BuildRequest` pattern. Add them after `GetRoomsAsync`, before `ParseRooms`.

- [ ] **Step 3.1 — Add `GetRoomsConfigurationsAsync`**

```csharp
public async Task<Dictionary<string, string>> GetRoomsConfigurationsAsync()
{
    using (var request = BuildRequest(HttpMethod.Get, "api/v1/rooms_configurations.json"))
    using (var response = await Http.SendAsync(request).ConfigureAwait(false))
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("Greenlight session expired or missing.");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        DebugLog.Write("GET /api/v1/rooms_configurations.json -> HTTP " + (int)response.StatusCode);
        return ParseConfigurations(body);
    }
}
```

- [ ] **Step 3.2 — Add `CreateRoomAsync`**

```csharp
public async Task<string> CreateRoomAsync(string name)
{
    var json = Newtonsoft.Json.JsonConvert.SerializeObject(
        new { room = new { name } });
    using (var request = BuildRequest(HttpMethod.Post, "api/v1/rooms.json"))
    {
        request.Content = new System.Net.Http.StringContent(
            json, System.Text.Encoding.UTF8, "application/json");
        using (var response = await Http.SendAsync(request).ConfigureAwait(false))
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Greenlight session expired or missing.");
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            DebugLog.Write("POST /api/v1/rooms.json -> HTTP " + (int)response.StatusCode
                + Environment.NewLine + body);
            return ExtractFriendlyId(body);
        }
    }
}
```

- [ ] **Step 3.3 — Add `UpdateRoomSettingAsync`**

```csharp
public async Task UpdateRoomSettingAsync(string friendlyId, string settingName, string settingValue)
{
    var json = Newtonsoft.Json.JsonConvert.SerializeObject(
        new { room_setting = new { settingName, settingValue } });
    using (var request = BuildRequest(HttpMethod.Patch,
        $"api/v1/room_settings/{friendlyId}.json"))
    {
        request.Content = new System.Net.Http.StringContent(
            json, System.Text.Encoding.UTF8, "application/json");
        using (var response = await Http.SendAsync(request).ConfigureAwait(false))
        {
            DebugLog.Write($"PATCH /api/v1/room_settings/{friendlyId}.json"
                + $" [{settingName}={settingValue}] -> HTTP " + (int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.Unauthorized
                || response.StatusCode == HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException("Greenlight session expired or missing.");
            response.EnsureSuccessStatusCode();
        }
    }
}
```

Note: `HttpMethod.Patch` requires .NET 4.5+; it is available on .NET 4.8.

- [ ] **Step 3.4 — Add `GetRoomSettingsAsync`**

```csharp
public async Task<Dictionary<string, string>> GetRoomSettingsAsync(string friendlyId)
{
    using (var request = BuildRequest(HttpMethod.Get,
        $"api/v1/room_settings/{friendlyId}.json"))
    using (var response = await Http.SendAsync(request).ConfigureAwait(false))
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized
            || response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("Greenlight session expired or missing.");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        DebugLog.Write($"GET /api/v1/room_settings/{friendlyId}.json -> HTTP "
            + (int)response.StatusCode);
        return ParseConfigurations(body);
    }
}
```

- [ ] **Step 3.5 — Build to verify no compile errors**

```
dotnet build src\GreenroomConnector\GreenroomConnector.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3.6 — Commit**

```
git add src/GreenroomConnector/Services/GreenlightClient.cs
git commit -m "Add GetRoomsConfigurations, CreateRoom, UpdateRoomSetting, GetRoomSettings to GreenlightClient"
```

---

## Task 4: Localization Strings

**Files:**
- Modify: `src/GreenroomConnector/Resources/Strings.resx`
- Modify: `src/GreenroomConnector/Resources/Strings.de.resx`

- [ ] **Step 4.1 — Add English strings to `Strings.resx`**

Insert after the `RoomPicker_CancelButton` entry (line 53):

```xml
  <data name="RoomPicker_NewRoomButton" xml:space="preserve"><value>New Room</value></data>
  <data name="RoomPicker_InsertAsModeratorButton" xml:space="preserve"><value>Insert as Moderator</value></data>
  <data name="RoomPicker_CheckingModeratorCode" xml:space="preserve"><value>Checking moderator code…</value></data>

  <data name="CreateRoom_Title" xml:space="preserve"><value>Create New Room</value></data>
  <data name="CreateRoom_NameLabel" xml:space="preserve"><value>Name</value></data>
  <data name="CreateRoom_SettingsGroup" xml:space="preserve"><value>Settings</value></data>
  <data name="CreateRoom_Record" xml:space="preserve"><value>Allow recording</value></data>
  <data name="CreateRoom_RequireAuth" xml:space="preserve"><value>Require authentication</value></data>
  <data name="CreateRoom_AnyoneModerator" xml:space="preserve"><value>Anyone can moderate</value></data>
  <data name="CreateRoom_ViewerCode" xml:space="preserve"><value>Viewer access code</value></data>
  <data name="CreateRoom_ModeratorCode" xml:space="preserve"><value>Moderator access code</value></data>
  <data name="CreateRoom_GuestPolicy" xml:space="preserve"><value>Guests</value></data>
  <data name="CreateRoom_GuestAlways" xml:space="preserve"><value>Always allow</value></data>
  <data name="CreateRoom_GuestAsk" xml:space="preserve"><value>Ask moderator</value></data>
  <data name="CreateRoom_GuestDeny" xml:space="preserve"><value>Always deny</value></data>
  <data name="CreateRoom_SubmitButton" xml:space="preserve"><value>Create &amp; Insert</value></data>
  <data name="CreateRoom_LoadingConfig" xml:space="preserve"><value>Loading settings…</value></data>
  <data name="CreateRoom_ConfigError" xml:space="preserve"><value>Could not load settings</value></data>
```

- [ ] **Step 4.2 — Add German strings to `Strings.de.resx`**

Insert after the `RoomPicker_CancelButton` entry (same position):

```xml
  <data name="RoomPicker_NewRoomButton" xml:space="preserve"><value>Neuen Raum anlegen</value></data>
  <data name="RoomPicker_InsertAsModeratorButton" xml:space="preserve"><value>Als Moderator einfügen</value></data>
  <data name="RoomPicker_CheckingModeratorCode" xml:space="preserve"><value>Moderator-Code wird geprüft…</value></data>

  <data name="CreateRoom_Title" xml:space="preserve"><value>Neuen Raum anlegen</value></data>
  <data name="CreateRoom_NameLabel" xml:space="preserve"><value>Name</value></data>
  <data name="CreateRoom_SettingsGroup" xml:space="preserve"><value>Einstellungen</value></data>
  <data name="CreateRoom_Record" xml:space="preserve"><value>Aufnahme erlauben</value></data>
  <data name="CreateRoom_RequireAuth" xml:space="preserve"><value>Nur angemeldete Nutzer</value></data>
  <data name="CreateRoom_AnyoneModerator" xml:space="preserve"><value>Alle als Moderator</value></data>
  <data name="CreateRoom_ViewerCode" xml:space="preserve"><value>Zugangscode für Zuschauer</value></data>
  <data name="CreateRoom_ModeratorCode" xml:space="preserve"><value>Zugangscode für Moderatoren</value></data>
  <data name="CreateRoom_GuestPolicy" xml:space="preserve"><value>Gäste</value></data>
  <data name="CreateRoom_GuestAlways" xml:space="preserve"><value>Immer erlauben</value></data>
  <data name="CreateRoom_GuestAsk" xml:space="preserve"><value>Moderator fragen</value></data>
  <data name="CreateRoom_GuestDeny" xml:space="preserve"><value>Immer ablehnen</value></data>
  <data name="CreateRoom_SubmitButton" xml:space="preserve"><value>Anlegen &amp; Einfügen</value></data>
  <data name="CreateRoom_LoadingConfig" xml:space="preserve"><value>Einstellungen werden geladen…</value></data>
  <data name="CreateRoom_ConfigError" xml:space="preserve"><value>Einstellungen konnten nicht geladen werden</value></data>
```

- [ ] **Step 4.3 — Rebuild to regenerate `Strings.Designer.cs`**

Open the solution in Visual Studio and rebuild, OR:

```
dotnet build src\GreenroomConnector\GreenroomConnector.csproj
```

The ResXFileCodeGenerator on `Strings.resx` will regenerate `Strings.Designer.cs` with the new properties. Verify `Strings.CreateRoom_Title` etc. are accessible.

- [ ] **Step 4.4 — Commit**

```
git add src/GreenroomConnector/Resources/Strings.resx src/GreenroomConnector/Resources/Strings.de.resx src/GreenroomConnector/Resources/Strings.Designer.cs
git commit -m "Add localization strings for room creation and moderator link"
```

---

## Task 5: `CreateRoomForm` Designer

**Files:**
- Create: `src/GreenroomConnector/UI/CreateRoomForm.Designer.cs`

- [ ] **Step 5.1 — Create the Designer file**

Create `src/GreenroomConnector/UI/CreateRoomForm.Designer.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace GreenroomConnector.UI
{
    partial class CreateRoomForm
    {
        private System.ComponentModel.IContainer components = null;
        private Label labelName;
        private TextBox textBoxName;
        private GroupBox groupBoxSettings;
        private CheckBox checkBoxRecord;
        private CheckBox checkBoxRequireAuth;
        private CheckBox checkBoxAnyoneModerator;
        private CheckBox checkBoxViewerCode;
        private CheckBox checkBoxModeratorCode;
        private GroupBox groupBoxGuestPolicy;
        private RadioButton radioButtonGuestAlways;
        private RadioButton radioButtonGuestAsk;
        private RadioButton radioButtonGuestDeny;
        private Panel footerSeparator;
        private Label labelStatus;
        private Button buttonCancel;
        private Button buttonCreate;

        private static readonly Color BrandBlue        = Color.FromArgb(15, 108, 189);
        private static readonly Color BrandBlueHover   = Color.FromArgb(17,  94, 163);
        private static readonly Color BrandBluePressed = Color.FromArgb(13,  81, 142);
        private static readonly Color SurfacePrimary   = Color.White;
        private static readonly Color SurfaceMuted     = Color.FromArgb(250, 250, 250);
        private static readonly Color StrokeQuiet      = Color.FromArgb(225, 225, 225);
        private static readonly Color TextPrimary      = Color.FromArgb(36,  36,  36);
        private static readonly Color TextMuted        = Color.FromArgb(97,  97,  97);

        private static readonly Font FontBody   = new Font("Segoe UI",  9F);
        private static readonly Font FontLabel  = new Font("Segoe UI",  9F, FontStyle.Regular);
        private static readonly Font FontButton = new Font("Segoe UI",  9F, FontStyle.Regular);

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelName           = new Label();
            this.textBoxName         = new TextBox();
            this.groupBoxSettings    = new GroupBox();
            this.checkBoxRecord      = new CheckBox();
            this.checkBoxRequireAuth = new CheckBox();
            this.checkBoxAnyoneModerator = new CheckBox();
            this.checkBoxViewerCode  = new CheckBox();
            this.checkBoxModeratorCode = new CheckBox();
            this.groupBoxGuestPolicy = new GroupBox();
            this.radioButtonGuestAlways = new RadioButton();
            this.radioButtonGuestAsk    = new RadioButton();
            this.radioButtonGuestDeny   = new RadioButton();
            this.footerSeparator     = new Panel();
            this.labelStatus         = new Label();
            this.buttonCancel        = new Button();
            this.buttonCreate        = new Button();

            this.groupBoxSettings.SuspendLayout();
            this.groupBoxGuestPolicy.SuspendLayout();
            this.SuspendLayout();

            // labelName
            this.labelName.AutoSize  = false;
            this.labelName.Font      = FontLabel;
            this.labelName.ForeColor = TextPrimary;
            this.labelName.Location  = new Point(20, 20);
            this.labelName.Size      = new Size(440, 16);
            this.labelName.Text      = "Name";

            // textBoxName
            this.textBoxName.BorderStyle = BorderStyle.FixedSingle;
            this.textBoxName.Font        = FontBody;
            this.textBoxName.Location    = new Point(20, 40);
            this.textBoxName.Size        = new Size(440, 24);
            this.textBoxName.TextChanged += new System.EventHandler(this.TextBoxName_TextChanged);

            // groupBoxSettings
            this.groupBoxSettings.Font      = FontBody;
            this.groupBoxSettings.ForeColor = TextPrimary;
            this.groupBoxSettings.Location  = new Point(20, 80);
            this.groupBoxSettings.Size      = new Size(440, 280);
            this.groupBoxSettings.Text      = "Settings";

            // checkBoxRecord
            this.checkBoxRecord.Font     = FontBody;
            this.checkBoxRecord.Location = new Point(12, 28);
            this.checkBoxRecord.Size     = new Size(300, 20);

            // checkBoxRequireAuth
            this.checkBoxRequireAuth.Font     = FontBody;
            this.checkBoxRequireAuth.Location = new Point(12, 52);
            this.checkBoxRequireAuth.Size     = new Size(300, 20);

            // checkBoxAnyoneModerator
            this.checkBoxAnyoneModerator.Font     = FontBody;
            this.checkBoxAnyoneModerator.Location = new Point(12, 76);
            this.checkBoxAnyoneModerator.Size     = new Size(300, 20);

            // checkBoxViewerCode
            this.checkBoxViewerCode.Font     = FontBody;
            this.checkBoxViewerCode.Location = new Point(12, 100);
            this.checkBoxViewerCode.Size     = new Size(300, 20);

            // checkBoxModeratorCode
            this.checkBoxModeratorCode.Font     = FontBody;
            this.checkBoxModeratorCode.Location = new Point(12, 124);
            this.checkBoxModeratorCode.Size     = new Size(300, 20);

            // groupBoxGuestPolicy
            this.groupBoxGuestPolicy.Font      = FontBody;
            this.groupBoxGuestPolicy.ForeColor = TextPrimary;
            this.groupBoxGuestPolicy.Location  = new Point(12, 152);
            this.groupBoxGuestPolicy.Size      = new Size(416, 112);
            this.groupBoxGuestPolicy.Text      = "Guests";

            // radioButtonGuestAlways
            this.radioButtonGuestAlways.Font     = FontBody;
            this.radioButtonGuestAlways.Location = new Point(12, 24);
            this.radioButtonGuestAlways.Size     = new Size(260, 20);
            this.radioButtonGuestAlways.Checked  = true;

            // radioButtonGuestAsk
            this.radioButtonGuestAsk.Font     = FontBody;
            this.radioButtonGuestAsk.Location = new Point(12, 48);
            this.radioButtonGuestAsk.Size     = new Size(260, 20);

            // radioButtonGuestDeny
            this.radioButtonGuestDeny.Font     = FontBody;
            this.radioButtonGuestDeny.Location = new Point(12, 72);
            this.radioButtonGuestDeny.Size     = new Size(260, 20);

            // footerSeparator
            this.footerSeparator.BackColor = StrokeQuiet;
            this.footerSeparator.Location  = new Point(0, 378);
            this.footerSeparator.Size      = new Size(480, 1);

            // labelStatus
            this.labelStatus.AutoSize  = false;
            this.labelStatus.Font      = FontBody;
            this.labelStatus.ForeColor = TextMuted;
            this.labelStatus.Location  = new Point(20, 386);
            this.labelStatus.Size      = new Size(440, 20);
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // buttonCancel
            this.buttonCancel.Font      = FontButton;
            this.buttonCancel.FlatStyle = FlatStyle.Flat;
            this.buttonCancel.BackColor = SurfacePrimary;
            this.buttonCancel.ForeColor = TextPrimary;
            this.buttonCancel.FlatAppearance.BorderColor       = StrokeQuiet;
            this.buttonCancel.FlatAppearance.BorderSize        = 1;
            this.buttonCancel.FlatAppearance.MouseOverBackColor  = SurfaceMuted;
            this.buttonCancel.FlatAppearance.MouseDownBackColor  = StrokeQuiet;
            this.buttonCancel.Cursor   = Cursors.Hand;
            this.buttonCancel.Location = new Point(366, 412);
            this.buttonCancel.Size     = new Size(94, 30);
            this.buttonCancel.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            this.buttonCancel.Click   += new System.EventHandler(this.ButtonCancel_Click);

            // buttonCreate (primary)
            this.buttonCreate.Font      = FontButton;
            this.buttonCreate.FlatStyle = FlatStyle.Flat;
            this.buttonCreate.BackColor = BrandBlue;
            this.buttonCreate.ForeColor = Color.White;
            this.buttonCreate.FlatAppearance.BorderColor       = BrandBlue;
            this.buttonCreate.FlatAppearance.BorderSize        = 1;
            this.buttonCreate.FlatAppearance.MouseOverBackColor  = BrandBlueHover;
            this.buttonCreate.FlatAppearance.MouseDownBackColor  = BrandBluePressed;
            this.buttonCreate.Cursor   = Cursors.Hand;
            this.buttonCreate.Location = new Point(266, 412);
            this.buttonCreate.Size     = new Size(94, 30);
            this.buttonCreate.Anchor   = AnchorStyles.Bottom | AnchorStyles.Right;
            this.buttonCreate.Enabled  = false;
            this.buttonCreate.Click   += new System.EventHandler(this.ButtonCreate_Click);

            // Add controls to groupBoxGuestPolicy
            this.groupBoxGuestPolicy.Controls.Add(this.radioButtonGuestAlways);
            this.groupBoxGuestPolicy.Controls.Add(this.radioButtonGuestAsk);
            this.groupBoxGuestPolicy.Controls.Add(this.radioButtonGuestDeny);

            // Add controls to groupBoxSettings
            this.groupBoxSettings.Controls.Add(this.checkBoxRecord);
            this.groupBoxSettings.Controls.Add(this.checkBoxRequireAuth);
            this.groupBoxSettings.Controls.Add(this.checkBoxAnyoneModerator);
            this.groupBoxSettings.Controls.Add(this.checkBoxViewerCode);
            this.groupBoxSettings.Controls.Add(this.checkBoxModeratorCode);
            this.groupBoxSettings.Controls.Add(this.groupBoxGuestPolicy);

            // Form
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode       = AutoScaleMode.Font;
            this.Font                = FontBody;
            this.BackColor           = SurfacePrimary;
            this.ClientSize          = new Size(480, 452);
            this.FormBorderStyle     = FormBorderStyle.FixedDialog;
            this.MinimizeBox         = false;
            this.MaximizeBox         = false;
            this.StartPosition       = FormStartPosition.CenterParent;
            this.ShowInTaskbar       = false;
            this.AcceptButton        = this.buttonCreate;
            this.CancelButton        = this.buttonCancel;

            this.Controls.Add(this.labelName);
            this.Controls.Add(this.textBoxName);
            this.Controls.Add(this.groupBoxSettings);
            this.Controls.Add(this.footerSeparator);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonCreate);

            this.groupBoxGuestPolicy.ResumeLayout(false);
            this.groupBoxSettings.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
```

- [ ] **Step 5.2 — Add file to project and build**

Add `CreateRoomForm.Designer.cs` to the Visual Studio project. Build to confirm no layout errors:

```
dotnet build src\GreenroomConnector\GreenroomConnector.csproj
```

Expected: Build succeeded (will fail until Step 6 adds the partial class).

---

## Task 6: `CreateRoomForm` Logic

**Files:**
- Create: `src/GreenroomConnector/UI/CreateRoomForm.cs`

- [ ] **Step 6.1 — Create `CreateRoomForm.cs`**

Create `src/GreenroomConnector/UI/CreateRoomForm.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using GreenroomConnector.Models;
using GreenroomConnector.Resources;
using GreenroomConnector.Services;

namespace GreenroomConnector.UI
{
    public partial class CreateRoomForm : Form
    {
        public Room CreatedRoom { get; private set; }

        private RoomConfiguration _config;

        public CreateRoomForm()
        {
            InitializeComponent();
            Icon      = AppIcon.Load();
            ShowIcon  = true;
            Text      = Strings.CreateRoom_Title;
            labelName.Text                = Strings.CreateRoom_NameLabel;
            groupBoxSettings.Text         = Strings.CreateRoom_SettingsGroup;
            checkBoxRecord.Text           = Strings.CreateRoom_Record;
            checkBoxRequireAuth.Text      = Strings.CreateRoom_RequireAuth;
            checkBoxAnyoneModerator.Text  = Strings.CreateRoom_AnyoneModerator;
            checkBoxViewerCode.Text       = Strings.CreateRoom_ViewerCode;
            checkBoxModeratorCode.Text    = Strings.CreateRoom_ModeratorCode;
            groupBoxGuestPolicy.Text      = Strings.CreateRoom_GuestPolicy;
            radioButtonGuestAlways.Text   = Strings.CreateRoom_GuestAlways;
            radioButtonGuestAsk.Text      = Strings.CreateRoom_GuestAsk;
            radioButtonGuestDeny.Text     = Strings.CreateRoom_GuestDeny;
            buttonCreate.Text             = Strings.CreateRoom_SubmitButton;
            buttonCancel.Text             = Strings.RoomPicker_CancelButton;
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadConfigAsync().ConfigureAwait(true);
        }

        private async Task LoadConfigAsync()
        {
            SetBusy(true, Strings.CreateRoom_LoadingConfig);
            try
            {
                var client = ThisAddIn.Instance.Client;
                if (!client.HasSession && !await TryLoginAsync().ConfigureAwait(true))
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }

                Dictionary<string, string> raw;
                try
                {
                    raw = await client.GetRoomsConfigurationsAsync().ConfigureAwait(true);
                }
                catch
                {
                    // Fallback: show all settings
                    raw = new Dictionary<string, string>();
                    labelStatus.Text = Strings.CreateRoom_ConfigError;
                    _config = new RoomConfiguration(raw);
                    ApplyConfigToControls(showAll: true);
                    SetBusy(false, null);
                    return;
                }

                _config = new RoomConfiguration(raw);
                ApplyConfigToControls(showAll: false);
            }
            catch (Exception ex)
            {
                labelStatus.Text = string.Format(Strings.Error_Unexpected, ex.Message);
            }
            finally
            {
                SetBusy(false, null);
            }
        }

        private void ApplyConfigToControls(bool showAll)
        {
            checkBoxRecord.Visible         = showAll || _config.CanChange("record");
            checkBoxRecord.Checked         = _config.IsDefaultEnabled("record");

            checkBoxRequireAuth.Visible    = showAll || _config.CanChange("glRequireAuthentication");
            checkBoxRequireAuth.Checked    = _config.IsDefaultEnabled("glRequireAuthentication");

            checkBoxAnyoneModerator.Visible = showAll || _config.CanChange("glAnyoneJoinAsModerator");
            checkBoxAnyoneModerator.Checked = _config.IsDefaultEnabled("glAnyoneJoinAsModerator");

            checkBoxViewerCode.Visible     = showAll || _config.CanToggleAccessCode("glViewerAccessCode");
            checkBoxViewerCode.Checked     = false;

            checkBoxModeratorCode.Visible  = showAll || _config.CanToggleAccessCode("glModeratorAccessCode");
            checkBoxModeratorCode.Checked  = false;

            groupBoxGuestPolicy.Visible    = showAll || _config.CanChange("guestPolicy");
            radioButtonGuestAlways.Checked = true;
        }

        private async void ButtonCreate_Click(object sender, EventArgs e)
        {
            var name = textBoxName.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            SetBusy(true, null);
            try
            {
                var client = ThisAddIn.Instance.Client;
                string friendlyId;
                try
                {
                    friendlyId = await client.CreateRoomAsync(name).ConfigureAwait(true);
                }
                catch (UnauthorizedAccessException)
                {
                    ThisAddIn.Instance.Session.Clear();
                    if (!await TryLoginAsync().ConfigureAwait(true))
                    {
                        SetBusy(false, null);
                        return;
                    }
                    friendlyId = await client.CreateRoomAsync(name).ConfigureAwait(true);
                }

                await ApplySettingsAsync(client, friendlyId).ConfigureAwait(true);

                var baseUrl = ThisAddIn.Instance.Settings.GreenlightUrl;
                CreatedRoom = new Room
                {
                    Name       = name,
                    FriendlyId = friendlyId,
                    JoinUrl    = new Uri(baseUrl, $"rooms/{friendlyId}").ToString()
                };

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                labelStatus.Text = string.Format(Strings.Error_Unexpected, ex.Message);
                SetBusy(false, null);
            }
        }

        private async Task ApplySettingsAsync(GreenlightClient client, string friendlyId)
        {
            var tasks = new List<(string name, string value)>();

            if (checkBoxRecord.Visible)
                tasks.Add(("record", checkBoxRecord.Checked ? "true" : "false"));
            if (checkBoxRequireAuth.Visible)
                tasks.Add(("glRequireAuthentication", checkBoxRequireAuth.Checked ? "true" : "false"));
            if (checkBoxAnyoneModerator.Visible)
                tasks.Add(("glAnyoneJoinAsModerator", checkBoxAnyoneModerator.Checked ? "true" : "false"));
            if (checkBoxViewerCode.Visible)
                tasks.Add(("glViewerAccessCode", checkBoxViewerCode.Checked ? "true" : "false"));
            if (checkBoxModeratorCode.Visible)
                tasks.Add(("glModeratorAccessCode", checkBoxModeratorCode.Checked ? "true" : "false"));
            if (groupBoxGuestPolicy.Visible)
            {
                var guestValue = radioButtonGuestAlways.Checked ? "ALWAYS_ACCEPT"
                               : radioButtonGuestAsk.Checked    ? "ASK_MODERATOR"
                               :                                  "ALWAYS_DENY";
                tasks.Add(("guestPolicy", guestValue));
            }

            foreach (var (settingName, settingValue) in tasks)
            {
                try
                {
                    await client.UpdateRoomSettingAsync(
                        friendlyId, settingName, settingValue).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    DebugLog.Write($"Setting '{settingName}' could not be applied: {ex.Message}");
                }
            }
        }

        private Task<bool> TryLoginAsync()
        {
            var baseUrl = ThisAddIn.Instance.Settings.GreenlightUrl;
            if (baseUrl == null)
            {
                MessageBox.Show(Strings.Error_NoGreenlightUrl, Strings.App_Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return Task.FromResult(false);
            }
            using (var login = new LoginWindow(baseUrl))
            {
                var result = login.ShowDialog(this);
                if (result != DialogResult.OK || string.IsNullOrEmpty(login.SessionCookie))
                    return Task.FromResult(false);
                ThisAddIn.Instance.Session.WriteCookie(login.SessionCookie);
                if (!string.IsNullOrEmpty(login.AuthorizeUrl))
                    ThisAddIn.Instance.Session.WriteAuthorizeUrl(login.AuthorizeUrl);
                return Task.FromResult(true);
            }
        }

        private void SetBusy(bool busy, string message)
        {
            textBoxName.Enabled      = !busy;
            groupBoxSettings.Enabled = !busy;
            buttonCreate.Enabled     = !busy && !string.IsNullOrWhiteSpace(textBoxName.Text);
            UseWaitCursor            = busy;
            if (message != null)
                labelStatus.Text = message;
            else if (!busy && labelStatus.Text == Strings.CreateRoom_LoadingConfig)
                labelStatus.Text = string.Empty;
        }

        private void TextBoxName_TextChanged(object sender, EventArgs e)
        {
            buttonCreate.Enabled = !string.IsNullOrWhiteSpace(textBoxName.Text);
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
```

- [ ] **Step 6.2 — Add to project and build**

Add `CreateRoomForm.cs` to the Visual Studio project. Build:

```
dotnet build src\GreenroomConnector\GreenroomConnector.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6.3 — Commit**

```
git add src/GreenroomConnector/UI/CreateRoomForm.cs src/GreenroomConnector/UI/CreateRoomForm.Designer.cs
git commit -m "Add CreateRoomForm with adaptive settings and room creation logic"
```

---

## Task 7: `RoomPickerForm` — "Neuen Raum anlegen" Button

**Files:**
- Modify: `src/GreenroomConnector/UI/RoomPickerForm.Designer.cs`
- Modify: `src/GreenroomConnector/UI/RoomPickerForm.cs`

- [ ] **Step 7.1 — Widen the form and add `buttonNewRoom` in Designer**

In `RoomPickerForm.Designer.cs`:

Add field declaration alongside existing buttons:
```csharp
private Button buttonNewRoom;
private Button buttonInsertAsModerator;
```

In `InitializeComponent()`, add after `this.buttonCancel = new Button();`:
```csharp
this.buttonNewRoom = new Button();
this.buttonInsertAsModerator = new Button();
```

Add button configurations before the `// Form` section:

```csharp
// New Room button (secondary, bottom-left)
this.buttonNewRoom.Font      = FontButton;
this.buttonNewRoom.FlatStyle = FlatStyle.Flat;
this.buttonNewRoom.BackColor = SurfacePrimary;
this.buttonNewRoom.ForeColor = TextPrimary;
this.buttonNewRoom.FlatAppearance.BorderColor      = StrokeQuiet;
this.buttonNewRoom.FlatAppearance.BorderSize       = 1;
this.buttonNewRoom.FlatAppearance.MouseOverBackColor = SurfaceMuted;
this.buttonNewRoom.FlatAppearance.MouseDownBackColor = StrokeQuiet;
this.buttonNewRoom.Cursor   = Cursors.Hand;
this.buttonNewRoom.Location = new Point(20, 348);
this.buttonNewRoom.Size     = new Size(140, 30);
this.buttonNewRoom.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
this.buttonNewRoom.Text     = "New Room";
this.buttonNewRoom.Click   += new System.EventHandler(this.ButtonNewRoom_Click);

// Insert as Moderator button (secondary, hidden by default)
this.buttonInsertAsModerator.Font      = FontButton;
this.buttonInsertAsModerator.FlatStyle = FlatStyle.Flat;
this.buttonInsertAsModerator.BackColor = SurfacePrimary;
this.buttonInsertAsModerator.ForeColor = TextPrimary;
this.buttonInsertAsModerator.FlatAppearance.BorderColor      = StrokeQuiet;
this.buttonInsertAsModerator.FlatAppearance.BorderSize       = 1;
this.buttonInsertAsModerator.FlatAppearance.MouseOverBackColor = SurfaceMuted;
this.buttonInsertAsModerator.FlatAppearance.MouseDownBackColor = StrokeQuiet;
this.buttonInsertAsModerator.Cursor   = Cursors.Hand;
this.buttonInsertAsModerator.Location = new Point(166, 348);
this.buttonInsertAsModerator.Size     = new Size(130, 30);
this.buttonInsertAsModerator.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
this.buttonInsertAsModerator.Text     = "Insert as Moderator";
this.buttonInsertAsModerator.Visible  = false;
// Click event wired in Task 8 once the handler exists
```

Update the existing button positions and form size (wider to fit four buttons):

```csharp
// Update buttonInsert location
this.buttonInsert.Location = new Point(326, 348);

// Update buttonCancel location  
this.buttonCancel.Location = new Point(426, 348);

// Update footerSeparator width
this.footerSeparator.Size = new Size(560, 1);

// Update labelHeader, listRooms, labelStatus widths
this.labelHeader.Size  = new Size(520, 28);
this.listRooms.Size    = new Size(520, 240);
this.labelStatus.Size  = new Size(520, 20);
```

Update the `// Form` section:
```csharp
this.ClientSize    = new Size(560, 388);
this.MinimumSize   = new Size(560, 320);
```

Add the new controls to `this.Controls.Add(...)` block:
```csharp
this.Controls.Add(this.buttonNewRoom);
this.Controls.Add(this.buttonInsertAsModerator);
```

- [ ] **Step 7.2 — Add `ButtonNewRoom_Click` handler to `RoomPickerForm.cs`**

Add this method to `RoomPickerForm.cs` after `ButtonCancel_Click`:

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

Also update the constructor to set the new button label:
```csharp
buttonNewRoom.Text = Strings.RoomPicker_NewRoomButton;
```

Add this line alongside the other button label assignments in the constructor.

- [ ] **Step 7.3 — Build to verify**

```
dotnet build src\GreenroomConnector\GreenroomConnector.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 7.4 — Commit**

```
git add src/GreenroomConnector/UI/RoomPickerForm.cs src/GreenroomConnector/UI/RoomPickerForm.Designer.cs
git commit -m "Add New Room button to RoomPickerForm"
```

---

## Task 8: `RoomPickerForm` — "Als Moderator einfügen" Button

**Files:**
- Modify: `src/GreenroomConnector/UI/RoomPickerForm.cs`

- [ ] **Step 8.1 — Add fields and constructor label to `RoomPickerForm.cs`**

Add these private fields at the top of the `RoomPickerForm` class:

```csharp
private string _moderatorCode;
private System.Threading.CancellationTokenSource _settingsCts;
```

In the constructor, add button label assignment alongside the others:
```csharp
buttonInsertAsModerator.Text = Strings.RoomPicker_InsertAsModeratorButton;
```

- [ ] **Step 8.2 — Wire events in Designer**

In `RoomPickerForm.Designer.cs`:

Add to the `listRooms` configuration block (after the existing `DoubleClick` line):
```csharp
this.listRooms.SelectedIndexChanged += new System.EventHandler(this.ListRooms_SelectedIndexChanged);
```

Replace the placeholder comment on `buttonInsertAsModerator` with the real event wire:
```csharp
this.buttonInsertAsModerator.Click += new System.EventHandler(this.ButtonInsertAsModerator_Click);
```

- [ ] **Step 8.3 — Add `ListRooms_SelectedIndexChanged` handler**

Add to `RoomPickerForm.cs`:

```csharp
private async void ListRooms_SelectedIndexChanged(object sender, EventArgs e)
{
    // Cancel any in-flight settings fetch for the previously selected room
    _settingsCts?.Cancel();
    _settingsCts?.Dispose();
    _settingsCts = new System.Threading.CancellationTokenSource();
    var cts = _settingsCts;

    buttonInsertAsModerator.Visible = false;
    _moderatorCode = null;

    var room = listRooms.SelectedItem as Room;
    if (room == null) return;

    buttonInsert.Enabled = false;
    labelStatus.Text = Strings.RoomPicker_CheckingModeratorCode;

    try
    {
        var settings = await ThisAddIn.Instance.Client
            .GetRoomSettingsAsync(room.FriendlyId)
            .ConfigureAwait(true);

        if (cts.IsCancellationRequested) return;

        settings.TryGetValue("glModeratorAccessCode", out var code);
        if (!string.IsNullOrEmpty(code))
        {
            _moderatorCode = code;
            buttonInsertAsModerator.Visible = true;
        }
    }
    catch (Exception ex)
    {
        if (!cts.IsCancellationRequested)
            DebugLog.Write("Could not fetch room settings for moderator check: " + ex.Message);
    }
    finally
    {
        if (!cts.IsCancellationRequested)
        {
            buttonInsert.Enabled = listRooms.Items.Count > 0;
            labelStatus.Text = string.Empty;
        }
    }
}
```

- [ ] **Step 8.4 — Add `ButtonInsertAsModerator_Click` handler**

Add to `RoomPickerForm.cs`:

```csharp
private void ButtonInsertAsModerator_Click(object sender, EventArgs e)
{
    var room = listRooms.SelectedItem as Room;
    if (room == null || string.IsNullOrEmpty(_moderatorCode)) return;

    SelectedRoom = room;
    SelectedRoom.JoinUrl = new Uri(
        ThisAddIn.Instance.Settings.GreenlightUrl,
        $"rooms/{room.FriendlyId}?viewerCode={_moderatorCode}"
    ).ToString();
    DialogResult = DialogResult.OK;
    Close();
}
```

- [ ] **Step 8.5 — Dispose `CancellationTokenSource` on form close**

Override `OnFormClosed` in `RoomPickerForm.cs`:

```csharp
protected override void OnFormClosed(FormClosedEventArgs e)
{
    _settingsCts?.Cancel();
    _settingsCts?.Dispose();
    base.OnFormClosed(e);
}
```

- [ ] **Step 8.6 — Build to verify**

```
dotnet build src\GreenroomConnector\GreenroomConnector.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 8.7 — Run all tests**

```
dotnet test tests\GreenroomConnector.Tests\
```

Expected: All tests pass.

- [ ] **Step 8.8 — Commit**

```
git add src/GreenroomConnector/UI/RoomPickerForm.cs src/GreenroomConnector/UI/RoomPickerForm.Designer.cs
git commit -m "Add Als Moderator button with proactive settings fetch and CancellationToken handling"
```

---

## Manual Smoke Test Checklist

Build the solution in Visual Studio (F5 into Outlook) and verify:

| Check | Verifies |
|---|---|
| "Neuen Raum anlegen" button visible bottom-left of picker | Designer layout correct |
| Clicking it opens `CreateRoomForm` with title "Neuen Raum anlegen" / "Create New Room" | Localization correct |
| Settings checkboxes appear only for optional/default_enabled configs | `ApplyConfigToControls` + `RoomConfiguration` correct |
| "Anlegen & Einfügen" disabled when name is empty, enabled when filled | `TextBoxName_TextChanged` correct |
| Creating a room inserts link into appointment and closes both dialogs | Full creation flow correct |
| Selecting a room with moderator code shows "Als Moderator einfügen" button | `ListRooms_SelectedIndexChanged` + `GetRoomSettingsAsync` correct |
| Selecting a room without moderator code hides the button | Conditional visibility correct |
| Switching rooms quickly doesn't show stale moderator button | `CancellationToken` race condition handling correct |
| "Als Moderator einfügen" inserts URL with `?viewerCode=...` | `ButtonInsertAsModerator_Click` URL construction correct |
| Standard "Einfügen" inserts plain URL unchanged | Existing path unaffected |
