using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using stdole;

namespace GreenroomConnector.Resources
{
    // Loads the embedded ribbon icon PNG and exposes it as an IPictureDisp,
    // which the Office Ribbon customUI getImage callback wants. The
    // GetIPictureDispFromPicture method on AxHost is protected, so we go
    // through a derived helper to access it.
    internal static class RibbonImageProvider
    {
        private const string ResourceName = "GreenroomConnector.Resources.RibbonIcon.png";

        private static Bitmap _bitmap;
        private static IPictureDisp _picture;

        public static IPictureDisp Get()
        {
            if (_picture != null) return _picture;
            var bmp = LoadBitmap();
            if (bmp == null) return null;
            _picture = (IPictureDisp)PictureDispConverter.GetIPictureDisp(bmp);
            return _picture;
        }

        private static Bitmap LoadBitmap()
        {
            if (_bitmap != null) return _bitmap;
            var asm = typeof(RibbonImageProvider).Assembly;
            using (var s = asm.GetManifestResourceStream(ResourceName))
            {
                if (s == null) return null;
                _bitmap = new Bitmap(s);
                return _bitmap;
            }
        }

        private sealed class PictureDispConverter : AxHost
        {
            private PictureDispConverter() : base("") { }

            public static object GetIPictureDisp(Image image)
            {
                return GetIPictureDispFromPicture(image);
            }
        }
    }
}
