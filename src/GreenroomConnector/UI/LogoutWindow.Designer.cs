using System.Drawing;
using System.Windows.Forms;

namespace GreenroomConnector.UI
{
    partial class LogoutWindow
    {
        private System.ComponentModel.IContainer components = null;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)(this.webView)).BeginInit();
            this.SuspendLayout();

            this.webView.CreationProperties = null;
            this.webView.DefaultBackgroundColor = Color.White;
            this.webView.Dock = DockStyle.Fill;
            this.webView.Location = new Point(0, 0);
            this.webView.Name = "webView";
            this.webView.Size = new Size(720, 540);
            this.webView.ZoomFactor = 1D;

            this.AutoScaleDimensions = new SizeF(7F, 15F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = new Font("Segoe UI", 9F);
            this.BackColor = Color.White;
            this.ClientSize = new Size(720, 540);
            this.Controls.Add(this.webView);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(480, 360);
            this.Name = "LogoutWindow";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowInTaskbar = false;
            this.Text = "Sign out";
            ((System.ComponentModel.ISupportInitialize)(this.webView)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
