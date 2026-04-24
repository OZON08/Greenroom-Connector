using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.Outlook;
using OutlookGreenlight.AddIn.Resources;
using OutlookGreenlight.AddIn.UI;

namespace OutlookGreenlight.AddIn
{
    [ComVisible(true)]
    public class RibbonHandler : IRibbonExtensibility
    {
        private IRibbonUI _ribbon;

        public string GetCustomUI(string ribbonID)
        {
            using (var stream = Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream("OutlookGreenlight.AddIn.Ribbon.xml"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                        return reader.ReadToEnd();
                }
            }

            var path = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                "Ribbon.xml");
            return File.ReadAllText(path);
        }

        public void OnRibbonLoad(IRibbonUI ribbon) => _ribbon = ribbon;

        public string OnGetGroupLabel(IRibbonControl control) => Strings.Ribbon_GroupLabel;
        public string OnGetButtonLabel(IRibbonControl control) => Strings.Ribbon_ButtonLabel;
        public string OnGetButtonSupertip(IRibbonControl control) => Strings.Ribbon_ButtonSupertip;

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
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.Error_Unexpected, ex.Message),
                    Strings.App_Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static AppointmentItem ResolveAppointment(IRibbonControl control)
        {
            if (control?.Context is Inspector inspector && inspector.CurrentItem is AppointmentItem fromInspector)
                return fromInspector;

            var app = ThisAddIn.Instance?.Application as Application;
            return app?.ActiveInspector()?.CurrentItem as AppointmentItem;
        }
    }
}
