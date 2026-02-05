using System;
using System.Text;
using UnityEngine;

namespace PicoShot.Localization.Rtl
{
    /// <summary>
    /// Public API for RTL text handling.
    /// </summary>
    public static class RtlTextHandler
    {
        private static readonly StringBuilder MultilineBuilder = new(1024);

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

            if (text.Contains("\n") && !text.Contains(Environment.NewLine))
            {
                text = text.Replace("\n", Environment.NewLine);
            }

            if (!text.Contains(Environment.NewLine))
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
            string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            
            if (lines.Length <= 1)
            {
                return RtlTextFixer.FixLine(text, options);
            }

            MultilineBuilder.Clear();
            MultilineBuilder.EnsureCapacity(text.Length);

            MultilineBuilder.Append(RtlTextFixer.FixLine(lines[0], options));

            for (int i = 1; i < lines.Length; i++)
            {
                MultilineBuilder.Append(Environment.NewLine);
                MultilineBuilder.Append(RtlTextFixer.FixLine(lines[i], options));
            }

            return MultilineBuilder.ToString();
        }
    }
}
