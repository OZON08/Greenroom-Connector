using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using GreenroomConnector.Models;
using GreenroomConnector.Resources;
using GreenroomConnector.Services;

namespace GreenroomConnector.UI
{
    public partial class RoomPickerForm : Form
    {
        public Room SelectedRoom { get; private set; }

        private string _moderatorCode;
        private System.Threading.CancellationTokenSource _settingsCts;

        public RoomPickerForm()
        {
            InitializeComponent();
            Icon = AppIcon.Load();
            ShowIcon = true;
            Text = Strings.RoomPicker_Title;
            labelHeader.Text = Strings.RoomPicker_Header;
            buttonInsert.Text = Strings.RoomPicker_InsertButton;
            buttonCancel.Text = Strings.RoomPicker_CancelButton;
            buttonNewRoom.Text           = Strings.RoomPicker_NewRoomButton;
            buttonInsertAsModerator.Text = Strings.RoomPicker_InsertAsModeratorButton;
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadRoomsAsync().ConfigureAwait(true);
        }

        private async Task LoadRoomsAsync()
        {
            listRooms.Items.Clear();
            SetBusy(true, Strings.RoomPicker_Loading);

            try
            {
                var client = ThisAddIn.Instance.Client;

                if (!client.HasSession && !await TryLoginAsync().ConfigureAwait(true))
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                    return;
                }

                IReadOnlyList<Room> rooms;
                try
                {
                    rooms = await client.GetRoomsAsync().ConfigureAwait(true);
                }
                catch (UnauthorizedAccessException)
                {
                    ThisAddIn.Instance.Session.Clear();
                    if (!await TryLoginAsync().ConfigureAwait(true))
                    {
                        DialogResult = DialogResult.Cancel;
                        Close();
                        return;
                    }
                    rooms = await client.GetRoomsAsync().ConfigureAwait(true);
                }

                foreach (var room in rooms)
                    listRooms.Items.Add(room);

                if (listRooms.Items.Count == 0)
                {
                    labelStatus.Text = Strings.RoomPicker_NoRooms;
                    buttonInsert.Enabled = false;
                }
                else
                {
                    labelStatus.Text = string.Empty;
                    listRooms.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                labelStatus.Text = string.Format(Strings.Error_LoadRooms, ex.Message);
                buttonInsert.Enabled = false;
            }
            finally
            {
                SetBusy(false, null);
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
            listRooms.Enabled = !busy;
            buttonInsert.Enabled = !busy && listRooms.Items.Count > 0;
            labelStatus.Text = message ?? string.Empty;
            UseWaitCursor = busy;
        }

        private void ButtonInsert_Click(object sender, EventArgs e)
        {
            SelectedRoom = listRooms.SelectedItem as Room;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

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

        private void ListRooms_DoubleClick(object sender, EventArgs e)
        {
            if (listRooms.SelectedItem is Room)
                ButtonInsert_Click(sender, e);
        }

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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _settingsCts?.Cancel();
            _settingsCts?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
