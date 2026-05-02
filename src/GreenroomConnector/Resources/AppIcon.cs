using System.Drawing;
using System.IO;
using System.Reflection;

namespace GreenroomConnector.Resources
{
    // Loads the embedded multi-size application icon. The same file
    // contains 16, 24, 32, 48, 64 and 128 px renderings — Windows picks
    // the right size for the title bar, Alt-Tab and HiDPI scaling.
    internal static class AppIcon
    {
        private const string ResourceName = "GreenroomConnector.Resources.AppIcon.ico";

        private static Icon _cached;

        public static Icon Load()
        {
            if (_cached != null) return _cached;
            var asm = typeof(AppIcon).Assembly;
            using (var stream = asm.GetManifestResourceStream(ResourceName))
            {
                if (stream == null) return null;
                _cached = new Icon(stream);
                return _cached;
            }
        }
    }
}
