using System;

namespace PicoShot.Localization
{
    /// <summary>
    /// Backward compatibility wrapper for RTL text handling.
    /// New code should use GameDevKit.Localization.Rtl.RtlTextHandler directly.
    /// </summary>
    [Obsolete("Use GameDevKit.Localization.Rtl.RtlTextHandler instead")]
    public static class LocalizationRtlManager
    {
        /// <summary>
        /// Fixes RTL text for proper display.
        /// </summary>
        public static string Fix(string str)
        {
            return Rtl.RtlTextHandler.Fix(str);
        }

        /// <summary>
        /// Fixes RTL text with custom options.
        /// </summary>
        public static string Fix(string str, bool showTashkeel, bool useHinduNumbers)
        {
            return Rtl.RtlTextHandler.Fix(str, new Rtl.RtlFixOptions
            {
                ShowTashkeel = showTashkeel,
                UseHinduNumbers = useHinduNumbers
            });
        }

        /// <summary>
        /// Fixes RTL text with full custom options.
        /// </summary>
        public static string Fix(string str, bool showTashkeel, bool combineTashkeel, bool useHinduNumbers)
        {
            return Rtl.RtlTextHandler.Fix(str, new Rtl.RtlFixOptions
            {
                ShowTashkeel = showTashkeel,
                UseHinduNumbers = useHinduNumbers
            });
        }
    }
}
