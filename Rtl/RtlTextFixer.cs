using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PicoShot.Localization.Rtl
{
    /// <summary>
    /// Configuration options for RTL text fixing.
    /// </summary>
    public struct RtlFixOptions
    {
        public bool ShowTashkeel;
        public bool UseHinduNumbers;

        public static readonly RtlFixOptions Default = new()
        {
            ShowTashkeel = true,
            UseHinduNumbers = false
        };
    }

    /// <summary>
    /// Core RTL text fixing logic.
    /// </summary>
    internal static class RtlTextFixer
    {
        private static readonly StringBuilder LineBuilder = new(1024);
        private static readonly List<char> NumberBuffer = new(16);

        /// <summary>
        /// Fixes a line of RTL text for proper display.
        /// </summary>
        public static string FixLine(string line, RtlFixOptions options)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            // Step 1: Remove tashkeel if needed
            List<TashkeelPosition> tashkeelPositions = null;
            if (!options.ShowTashkeel)
            {
                line = TashkeelHandler.RemoveTashkeel(line, out tashkeelPositions);
            }

            // Step 2: Convert to isolated forms
            char[] isolatedChars = new char[line.Length];
            for (int i = 0; i < line.Length; i++)
            {
                isolatedChars[i] = (char)ArabicLetterConverter.ToIsolated(line[i]);
            }

            // Step 3: Handle Lam-Alef combinations
            char[] connectedChars = new char[isolatedChars.Length];
            for (int i = 0; i < isolatedChars.Length; i++)
            {
                connectedChars[i] = isolatedChars[i];

                if (i < isolatedChars.Length - 1 && 
                    ArabicGlyphConnector.TryCombineLamAlef(
                        isolatedChars[i], 
                        isolatedChars[i + 1], 
                        out var combinedLam, 
                        out var nextOutput))
                {
                    connectedChars[i] = combinedLam;
                    connectedChars[i + 1] = nextOutput;
                    i++;
                }
            }

            // Step 4: Apply glyph forms based on position
            char[] finalChars = new char[connectedChars.Length];
            for (int i = 0; i < connectedChars.Length; i++)
            {
                if (ArabicGlyphConnector.IsIgnoredCharacter(connectedChars[i]))
                {
                    finalChars[i] = connectedChars[i];
                    continue;
                }

                var position = ArabicGlyphConnector.GetLetterPosition(connectedChars, i);
                finalChars[i] = ArabicGlyphConnector.GetGlyphForm(connectedChars[i], position);

                // Handle Hindu numbers
                if (options.UseHinduNumbers)
                {
                    finalChars[i] = ConvertToHinduNumber(finalChars[i]);
                }
            }

            // Step 5: Restore tashkeel if needed
            if (options.ShowTashkeel && tashkeelPositions != null)
            {
                TashkeelHandler.RestoreTashkeel(ref finalChars, tashkeelPositions);
            }

            // Step 6: Reverse and reorder (RTL layout)
            return ReorderForRtl(finalChars);
        }

        /// <summary>
        /// Reorders characters for RTL display, handling embedded LTR text (numbers, Latin).
        /// </summary>
        private static string ReorderForRtl(char[] chars)
        {
            LineBuilder.Clear();
            LineBuilder.EnsureCapacity(chars.Length);
            NumberBuffer.Clear();

            // Process from end to beginning (since it's RTL)
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                char c = chars[i];

                // Handle special marker for skipped characters (Lam-Alef combination)
                if (c == (char)0xFFFF)
                    continue;

                // Handle brackets (flip them)
                if (IsBracket(c))
                {
                    FlushNumberBuffer();
                    LineBuilder.Append(FlipBracket(c));
                    continue;
                }

                // Handle Latin characters and numbers (keep LTR order within the RTL text)
                if (IsLatinChar(c) || char.IsSymbol(c) || char.IsSurrogate(c))
                {
                    NumberBuffer.Add(c);
                    continue;
                }

                // Regular RTL character
                FlushNumberBuffer();
                LineBuilder.Append(c);
            }

            FlushNumberBuffer();
            return LineBuilder.ToString();

            void FlushNumberBuffer()
            {
                if (NumberBuffer.Count == 0) return;

                // Numbers/Latin are LTR, so reverse them back
                for (int j = NumberBuffer.Count - 1; j >= 0; j--)
                {
                    LineBuilder.Append(NumberBuffer[j]);
                }
                NumberBuffer.Clear();
            }
        }

        private static bool IsLatinChar(char c)
        {
            return char.IsNumber(c) || char.IsLower(c) || char.IsUpper(c);
        }

        private static bool IsBracket(char c)
        {
            return c is '(' or ')' or '<' or '>' or '[' or ']';
        }

        private static char FlipBracket(char c)
        {
            return c switch
            {
                '(' => ')',
                ')' => '(',
                '<' => '>',
                '>' => '<',
                '[' => ']',
                ']' => '[',
                _ => c
            };
        }

        private static char ConvertToHinduNumber(char c)
        {
            return c switch
            {
                '0' => (char)0x0660,
                '1' => (char)0x0661,
                '2' => (char)0x0662,
                '3' => (char)0x0663,
                '4' => (char)0x0664,
                '5' => (char)0x0665,
                '6' => (char)0x0666,
                '7' => (char)0x0667,
                '8' => (char)0x0668,
                '9' => (char)0x0669,
                _ => c
            };
        }
    }
}
