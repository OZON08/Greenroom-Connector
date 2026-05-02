using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using GreenroomConnector.Models;
using GreenroomConnector.Resources;

namespace GreenroomConnector.UI
{
    public partial class RoomPickerForm : Form
    {
        public Room SelectedRoom { get; private set; }

        public RoomPickerForm()
        {
            InitializeComponent();
            Icon = AppIcon.Load();
            ShowIcon = true;
            Text = Strings.RoomPicker_Title;
            labelHeader.Text = Strings.RoomPicker_Header;
            buttonInsert.Text = Strings.RoomPicker_InsertButton;
            buttonCancel.Text = Strings.RoomPicker_CancelButton;
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

        private void ListRooms_DoubleClick(object sender, EventArgs e)
        {
            if (listRooms.SelectedItem is Room)
                ButtonInsert_Click(sender, e);
        }
    }
}
