using System.Buffers;
using System.Collections.Generic;
using System.Text;

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
    /// Thread-safe implementation using ArrayPool for reduced allocations.
    /// </summary>
    internal static class RtlTextFixer
    {
        /// <summary>
        /// Fixes a line of RTL text for proper display.
        /// </summary>
        internal static string FixLine(string line, RtlFixOptions options)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            List<TashkeelPosition> tashkeelPositions = null;
            string processedLine = line;

            if (!options.ShowTashkeel)
            {
                processedLine = TashkeelHandler.RemoveTashkeel(line, out tashkeelPositions);
            }

            int length = processedLine.Length;

            // Rent arrays from pool to avoid allocations
            char[] isolatedChars = ArrayPool<char>.Shared.Rent(length);
            char[] connectedChars = ArrayPool<char>.Shared.Rent(length);
            char[] finalChars = ArrayPool<char>.Shared.Rent(length + (tashkeelPositions?.Count ?? 0));

            try
            {
                // Step 1: Convert to isolated forms
                for (int i = 0; i < length; i++)
                {
                    isolatedChars[i] = (char)ArabicLetterConverter.ToIsolated(processedLine[i]);
                }

                // Step 2: Connect letters and handle Lam-Alef
                int connectedLength = 0;
                for (int i = 0; i < length; i++)
                {
                    if (i < length - 1 &&
                        ArabicGlyphConnector.TryCombineLamAlef(
                            isolatedChars[i],
                            isolatedChars[i + 1],
                            out char combinedLam,
                            out char nextOutput))
                    {
                        connectedChars[connectedLength++] = combinedLam;
                        connectedChars[connectedLength++] = nextOutput;
                        i++; // Skip next char as it's part of the ligature
                    }
                    else
                    {
                        connectedChars[connectedLength++] = isolatedChars[i];
                    }
                }

                // Step 3: Apply glyph forms based on position
                int finalLength = 0;
                for (int i = 0; i < connectedLength; i++)
                {
                    char c = connectedChars[i];

                    if (ArabicGlyphConnector.IsIgnoredCharacter(c))
                    {
                        finalChars[finalLength++] = c;
                        continue;
                    }

                    var position = ArabicGlyphConnector.GetLetterPosition(connectedChars, i, connectedLength);
                    char glyphForm = ArabicGlyphConnector.GetGlyphForm(c, position);

                    if (options.UseHinduNumbers)
                    {
                        glyphForm = ConvertToHinduNumber(glyphForm);
                    }

                    finalChars[finalLength++] = glyphForm;
                }

                // Step 4: Restore tashkeel if needed
                if (options.ShowTashkeel && tashkeelPositions != null)
                {
                    TashkeelHandler.RestoreTashkeel(finalChars, ref finalLength, tashkeelPositions);
                }

                // Step 5: Reorder for RTL display
                return ReorderForRtl(finalChars, finalLength);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(isolatedChars);
                ArrayPool<char>.Shared.Return(connectedChars);
                ArrayPool<char>.Shared.Return(finalChars);
            }
        }

        /// <summary>
        /// Reorders characters for RTL display, handling embedded LTR text (numbers, Latin).
        /// </summary>
        private static string ReorderForRtl(char[] chars, int length)
        {
            var sb = new StringBuilder(length);
            var numberBuffer = new List<char>(16);

            for (int i = length - 1; i >= 0; i--)
            {
                char c = chars[i];

                if (c == (char)0xFFFF)
                    continue;

                if (IsBracket(c))
                {
                    FlushNumberBuffer(sb, numberBuffer);
                    sb.Append(FlipBracket(c));
                    continue;
                }

                if (IsLatinChar(c) || char.IsSymbol(c) || char.IsSurrogate(c))
                {
                    numberBuffer.Add(c);
                    continue;
                }

                FlushNumberBuffer(sb, numberBuffer);
                sb.Append(c);
            }

            FlushNumberBuffer(sb, numberBuffer);
            return sb.ToString();
        }

        private static void FlushNumberBuffer(StringBuilder sb, List<char> buffer)
        {
            if (buffer.Count == 0) return;

            for (int j = buffer.Count - 1; j >= 0; j--)
            {
                sb.Append(buffer[j]);
            }
            buffer.Clear();
        }

        private static bool IsLatinChar(char c)
        {
            return char.IsNumber(c) || char.IsLower(c) || char.IsUpper(c);
        }

        private static bool IsBracket(char c)
        {
            return c is '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}';
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
                '{' => '}',
                '}' => '{',
                _ => c
            };
        }

        private static char ConvertToHinduNumber(char c)
        {
            return c switch
            {
                '0' => '\u0660',
                '1' => '\u0661',
                '2' => '\u0662',
                '3' => '\u0663',
                '4' => '\u0664',
                '5' => '\u0665',
                '6' => '\u0666',
                '7' => '\u0667',
                '8' => '\u0668',
                '9' => '\u0669',
                _ => c
            };
        }
    }
}
