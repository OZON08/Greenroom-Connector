using System.Drawing;
using System.Windows.Forms;

namespace GreenroomConnector.UI
{
    partial class RoomPickerForm
    {
        private System.ComponentModel.IContainer components = null;
        private Label labelHeader;
        private ListBox listRooms;
        private Label labelStatus;
        private Button buttonInsert;
        private Button buttonCancel;
        private Panel footerSeparator;

        // Microsoft 365 / Fluent palette
        private static readonly Color BrandBlue = Color.FromArgb(15, 108, 189);    // #0F6CBD
        private static readonly Color BrandBlueHover = Color.FromArgb(17, 94, 163); // #115EA3
        private static readonly Color BrandBluePressed = Color.FromArgb(13, 81, 142);
        private static readonly Color SurfacePrimary = Color.White;
        private static readonly Color SurfaceMuted = Color.FromArgb(250, 250, 250); // #FAFAFA
        private static readonly Color StrokeQuiet = Color.FromArgb(225, 225, 225);  // #E1E1E1
        private static readonly Color TextPrimary = Color.FromArgb(36, 36, 36);     // #242424
        private static readonly Color TextMuted = Color.FromArgb(97, 97, 97);       // #616161

        private static readonly Font FontBody = new Font("Segoe UI", 9F);
        private static readonly Font FontHeader = new Font("Segoe UI", 11F, FontStyle.Bold);
        private static readonly Font FontButton = new Font("Segoe UI", 9F, FontStyle.Regular);

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelHeader = new Label();
            this.listRooms = new ListBox();
            this.labelStatus = new Label();
            this.buttonInsert = new Button();
            this.buttonCancel = new Button();
            this.footerSeparator = new Panel();
            this.SuspendLayout();

            // Header label
            this.labelHeader.AutoSize = false;
            this.labelHeader.Font = FontHeader;
            this.labelHeader.ForeColor = TextPrimary;
            this.labelHeader.BackColor = Color.Transparent;
            this.labelHeader.Location = new Point(20, 20);
            this.labelHeader.Size = new Size(440, 28);
            this.labelHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.labelHeader.Text = "Header";

            // Room list
            this.listRooms.BorderStyle = BorderStyle.FixedSingle;
            this.listRooms.Font = FontBody;
            this.listRooms.ForeColor = TextPrimary;
            this.listRooms.BackColor = SurfacePrimary;
            this.listRooms.IntegralHeight = false;
            this.listRooms.ItemHeight = 22;
            this.listRooms.FormattingEnabled = true;
            this.listRooms.Location = new Point(20, 56);
            this.listRooms.Size = new Size(440, 240);
            this.listRooms.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                                  | AnchorStyles.Left | AnchorStyles.Right;
            this.listRooms.DoubleClick += new System.EventHandler(this.ListRooms_DoubleClick);

            // Status label
            this.labelStatus.AutoSize = false;
            this.labelStatus.Font = FontBody;
            this.labelStatus.ForeColor = TextMuted;
            this.labelStatus.BackColor = Color.Transparent;
            this.labelStatus.Location = new Point(20, 304);
            this.labelStatus.Size = new Size(440, 20);
            this.labelStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.labelStatus.TextAlign = ContentAlignment.MiddleLeft;

            // Footer separator (subtle hairline above buttons)
            this.footerSeparator.BackColor = StrokeQuiet;
            this.footerSeparator.Location = new Point(0, 332);
            this.footerSeparator.Size = new Size(480, 1);
            this.footerSeparator.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // Cancel button (secondary)
            this.buttonCancel.Font = FontButton;
            this.buttonCancel.FlatStyle = FlatStyle.Flat;
            this.buttonCancel.BackColor = SurfacePrimary;
            this.buttonCancel.ForeColor = TextPrimary;
            this.buttonCancel.FlatAppearance.BorderColor = StrokeQuiet;
            this.buttonCancel.FlatAppearance.BorderSize = 1;
            this.buttonCancel.FlatAppearance.MouseOverBackColor = SurfaceMuted;
            this.buttonCancel.FlatAppearance.MouseDownBackColor = StrokeQuiet;
            this.buttonCancel.Cursor = Cursors.Hand;
            this.buttonCancel.Location = new Point(366, 348);
            this.buttonCancel.Size = new Size(94, 30);
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);

            // Insert button (primary, accent blue)
            this.buttonInsert.Font = FontButton;
            this.buttonInsert.FlatStyle = FlatStyle.Flat;
            this.buttonInsert.BackColor = BrandBlue;
            this.buttonInsert.ForeColor = Color.White;
            this.buttonInsert.FlatAppearance.BorderColor = BrandBlue;
            this.buttonInsert.FlatAppearance.BorderSize = 1;
            this.buttonInsert.FlatAppearance.MouseOverBackColor = BrandBlueHover;
            this.buttonInsert.FlatAppearance.MouseDownBackColor = BrandBluePressed;
            this.buttonInsert.Cursor = Cursors.Hand;
            this.buttonInsert.Location = new Point(266, 348);
            this.buttonInsert.Size = new Size(94, 30);
            this.buttonInsert.Text = "Insert";
            this.buttonInsert.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.buttonInsert.Click += new System.EventHandler(this.ButtonInsert_Click);

            // Form
            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = FontBody;
            this.BackColor = SurfacePrimary;
            this.ClientSize = new Size(480, 388);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonInsert);
            this.Controls.Add(this.footerSeparator);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.listRooms);
            this.Controls.Add(this.labelHeader);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.MinimumSize = new Size(420, 320);
            this.Name = "RoomPickerForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Text = "Greenroom Connector";
            this.AcceptButton = this.buttonInsert;
            this.CancelButton = this.buttonCancel;
            this.Padding = new Padding(0);
            this.ResumeLayout(false);
        }
    }
}
