using System.Globalization;
using System.Threading;

namespace OutlookGreenlight.AddIn.Resources
{
    internal static class Localization
    {
        public static void ApplyCulture(string configured)
        {
            CultureInfo culture;
            switch ((configured ?? "auto").Trim().ToLowerInvariant())
            {
                case "de":
                    culture = new CultureInfo("de");
                    break;
                case "en":
                    culture = new CultureInfo("en");
                    break;
                default:
                    culture = CultureInfo.CurrentUICulture;
                    break;
            }

            Strings.Culture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}
