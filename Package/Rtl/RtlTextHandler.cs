using System;
using System.Text;

namespace PicoShot.Localization.Rtl
{
    /// <summary>
    /// Public API for RTL text handling.
    /// </summary>
    public static class RtlTextHandler
    {
        /// <summary>
        /// Fixes RTL text for proper display.
        /// </summary>
        /// <param name="text">The text to fix.</param>
        /// <returns>Fixed text ready for display.</returns>
        public static string Fix(string text)
        {
            return Fix(text, RtlFixOptions.Default);
        }

        /// <summary>
        /// Fixes RTL text with custom options.
        /// </summary>
        public static string Fix(string text, RtlFixOptions options)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (text.IndexOf('\n') < 0 && text.IndexOf("\r\n", StringComparison.Ordinal) < 0)
            {
                return RtlTextFixer.FixLine(text, options);
            }

            return FixMultiline(text, options);
        }

        /// <summary>
        /// Fixes multi-line RTL text, handling each line separately.
        /// </summary>
        private static string FixMultiline(string text, RtlFixOptions options)
        {
            // Normalize line endings to \n for consistent processing
            string normalized = text.Replace("\r\n", "\n");
            string[] lines = normalized.Split('\n');

            if (lines.Length <= 1)
            {
                return RtlTextFixer.FixLine(normalized, options);
            }

            var sb = new StringBuilder(text.Length + lines.Length);
            sb.Append(RtlTextFixer.FixLine(lines[0], options));

            for (int i = 1; i < lines.Length; i++)
            {
                sb.Append('\n');
                sb.Append(RtlTextFixer.FixLine(lines[i], options));
            }

            return sb.ToString();
        }
    }
}
