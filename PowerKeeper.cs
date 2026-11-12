using System.Runtime.InteropServices;

namespace KeepAwake
{
    internal static class PowerKeeper
    {
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        // Keeps the system awake. ES_DISPLAY_REQUIRED is deliberately not set, so
        // the display can turn off and the screensaver can start as usual.
        public static void BlockSleep()
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
        }

        public static void AllowSleep()
        {
            SetThreadExecutionState(ES_CONTINUOUS);
        }
    }
}
