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
