using System.Drawing;
using System.IO;

namespace KeepAwake
{
    // The window icon lives inside the assembly, so opening a form in the designer
    // never depends on whichever executable happens to be hosting it.
    internal static class AppIcon
    {
        private const string ResourceName = "KeepAwake.app.ico";

        private static Icon _value;

        public static Icon Value
        {
            get
            {
                if (_value == null)
                {
                    Stream stream = typeof(AppIcon).Assembly.GetManifestResourceStream(ResourceName);
                    _value = stream != null ? new Icon(stream) : SystemIcons.Application;
                }

                return _value;
            }
        }
    }
}
