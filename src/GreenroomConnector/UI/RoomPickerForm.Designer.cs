namespace GreenroomConnector.UI
{
    partial class RoomPickerForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label labelHeader;
        private System.Windows.Forms.ListBox listRooms;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.Button buttonInsert;
        private System.Windows.Forms.Button buttonCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelHeader = new System.Windows.Forms.Label();
            this.listRooms = new System.Windows.Forms.ListBox();
            this.labelStatus = new System.Windows.Forms.Label();
            this.buttonInsert = new System.Windows.Forms.Button();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();

            this.labelHeader.AutoSize = true;
            this.labelHeader.Location = new System.Drawing.Point(12, 12);
            this.labelHeader.Name = "labelHeader";
            this.labelHeader.Size = new System.Drawing.Size(80, 15);
            this.labelHeader.Text = "Header";

            this.listRooms.FormattingEnabled = true;
            this.listRooms.ItemHeight = 15;
            this.listRooms.Location = new System.Drawing.Point(12, 36);
            this.listRooms.Name = "listRooms";
            this.listRooms.Size = new System.Drawing.Size(440, 244);
            this.listRooms.Anchor = System.Windows.Forms.AnchorStyles.Top
                | System.Windows.Forms.AnchorStyles.Bottom
                | System.Windows.Forms.AnchorStyles.Left
                | System.Windows.Forms.AnchorStyles.Right;
            this.listRooms.DoubleClick += new System.EventHandler(this.ListRooms_DoubleClick);

            this.labelStatus.AutoSize = true;
            this.labelStatus.ForeColor = System.Drawing.Color.DimGray;
            this.labelStatus.Location = new System.Drawing.Point(12, 290);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(0, 15);
            this.labelStatus.Anchor = System.Windows.Forms.AnchorStyles.Bottom
                | System.Windows.Forms.AnchorStyles.Left;

            this.buttonInsert.Location = new System.Drawing.Point(296, 320);
            this.buttonInsert.Name = "buttonInsert";
            this.buttonInsert.Size = new System.Drawing.Size(75, 23);
            this.buttonInsert.Text = "Insert";
            this.buttonInsert.UseVisualStyleBackColor = true;
            this.buttonInsert.Anchor = System.Windows.Forms.AnchorStyles.Bottom
                | System.Windows.Forms.AnchorStyles.Right;
            this.buttonInsert.Click += new System.EventHandler(this.ButtonInsert_Click);

            this.buttonCancel.Location = new System.Drawing.Point(377, 320);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom
                | System.Windows.Forms.AnchorStyles.Right;
            this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(464, 355);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonInsert);
            this.Controls.Add(this.labelStatus);
            this.Controls.Add(this.listRooms);
            this.Controls.Add(this.labelHeader);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(360, 280);
            this.Name = "RoomPickerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Text = "Greenlight";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
