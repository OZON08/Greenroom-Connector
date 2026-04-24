using System;
using Microsoft.Office.Interop.Outlook;
using Microsoft.Office.Core;
using OutlookGreenlight.AddIn.Resources;
using OutlookGreenlight.AddIn.Services;

namespace OutlookGreenlight.AddIn
{
    public partial class ThisAddIn
    {
        internal static ThisAddIn Instance { get; private set; }

        internal SettingsProvider Settings { get; private set; }
        internal SessionStore Session { get; private set; }
        internal GreenlightClient Client { get; private set; }
        internal AppointmentWriter Writer { get; private set; }

        private void ThisAddIn_Startup(object sender, EventArgs e)
        {
            Instance = this;
            Settings = new SettingsProvider();
            Session = new SessionStore();
            Client = new GreenlightClient(Settings, Session);
            Writer = new AppointmentWriter();

            Localization.ApplyCulture(Settings.Language);
        }

        private void ThisAddIn_Shutdown(object sender, EventArgs e)
        {
            Client?.Dispose();
            Instance = null;
        }

        protected override IRibbonExtensibility CreateRibbonExtensibilityObject()
        {
            return new RibbonHandler();
        }

        #region VSTO generated code

        private void InternalStartup()
        {
            this.Startup += new EventHandler(ThisAddIn_Startup);
            this.Shutdown += new EventHandler(ThisAddIn_Shutdown);
        }

        #endregion
    }
}
