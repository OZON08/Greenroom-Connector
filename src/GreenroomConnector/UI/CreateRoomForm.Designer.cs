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
