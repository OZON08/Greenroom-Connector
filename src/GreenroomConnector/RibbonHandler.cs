using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Office.Core;
using GreenroomConnector.Resources;
using GreenroomConnector.UI;
using Outlook = Microsoft.Office.Interop.Outlook;
using stdole;

namespace GreenroomConnector
{
    [ComVisible(true)]
    public class RibbonHandler : IRibbonExtensibility
    {
        private IRibbonUI _ribbon;

        public string GetCustomUI(string ribbonID)
        {
            const string resource = "GreenroomConnector.Ribbon.xml";
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        "Embedded ribbon resource '" + resource + "' not found.");
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

        public void OnRibbonLoad(IRibbonUI ribbon) => _ribbon = ribbon;

        public string OnGetGroupLabel(IRibbonControl control) => Strings.Ribbon_GroupLabel;
        public string OnGetButtonLabel(IRibbonControl control) => Strings.Ribbon_ButtonLabel;
        public string OnGetButtonSupertip(IRibbonControl control) => Strings.Ribbon_ButtonSupertip;

        // Office Ribbon getImage callback. Returns the embedded PNG as an
        // IPictureDisp — the COM type the Office customUI expects. Bitmap
        // alone works on some Office builds but IPictureDisp is the safe path.
        public IPictureDisp OnGetButtonImage(IRibbonControl control) => RibbonImageProvider.Get();

        public void OnInsertGreenlightLink(IRibbonControl control)
        {
            try
            {
                var appointment = ResolveAppointment(control);
                if (appointment == null)
                {
                    MessageBox.Show(Strings.Error_NoAppointmentContext,
                        Strings.App_Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (var picker = new RoomPickerForm())
                {
                    if (picker.ShowDialog() != DialogResult.OK || picker.SelectedRoom == null)
                        return;

                    ThisAddIn.Instance.Writer.InsertMeetingLink(appointment, picker.SelectedRoom);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.Error_Unexpected, ex.Message),
                    Strings.App_Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Outlook.AppointmentItem ResolveAppointment(IRibbonControl control)
        {
            if (control?.Context is Outlook.Inspector inspector
                && inspector.CurrentItem is Outlook.AppointmentItem fromInspector)
            {
                return fromInspector;
            }

            var app = ThisAddIn.Instance?.Application;
            return app?.ActiveInspector()?.CurrentItem as Outlook.AppointmentItem;
        }
    }
}
