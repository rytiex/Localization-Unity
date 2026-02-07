using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace PicoShot.Localization.Rtl
{
    /// <summary>
    /// Represents a tashkeel (diacritical mark) position in text.
    /// </summary>
    internal readonly struct TashkeelPosition
    {
        public readonly char Tashkeel;
        public readonly int Position;

        public TashkeelPosition(char tashkeel, int position)
        {
            Tashkeel = tashkeel;
            Position = position;
        }
    }

    /// <summary>
    /// Handles removal and restoration of Arabic tashkeel (diacritical marks).
    /// Thread-safe implementation.
    /// </summary>
    internal static class TashkeelHandler
    {
        private const char TanweenFatha = '\u064B';
        private const char TanweenDamma = '\u064C';
        private const char TanweenKasra = '\u064D';
        private const char Fatha = '\u064E';
        private const char Damma = '\u064F';
        private const char Kasra = '\u0650';
        private const char Shadda = '\u0651';
        private const char Sukun = '\u0652';
        private const char Maddah = '\u0653';

        private const char CombinedFathaShadda = '\uFC60';
        private const char CombinedDammaShadda = '\uFC61';
        private const char CombinedKasraShadda = '\uFC62';

        /// <summary>
        /// Removes tashkeel from the string and stores positions for later restoration.
        /// </summary>
        public static string RemoveTashkeel(string input, out List<TashkeelPosition> positions)
        {
            positions = new List<TashkeelPosition>(input.Length / 8);
            
            if (string.IsNullOrEmpty(input))
                return input;

            var sb = new StringBuilder(input.Length);
            int lastSplitIndex = 0;

            for (int i = 0; i < input.Length; i++)
            {
                char currentChar = input[i];
                bool shouldRemove = false;

                switch (currentChar)
                {
                    case TanweenFatha:
                        positions.Add(new TashkeelPosition(TanweenFatha, i));
                        shouldRemove = true;
                        break;
                    case TanweenDamma:
                        positions.Add(new TashkeelPosition(TanweenDamma, i));
                        shouldRemove = true;
                        break;
                    case TanweenKasra:
                        positions.Add(new TashkeelPosition(TanweenKasra, i));
                        shouldRemove = true;
                        break;
                    case Fatha:
                        positions.Add(new TashkeelPosition(Fatha, i));
                        shouldRemove = true;
                        break;
                    case Damma:
                        positions.Add(new TashkeelPosition(Damma, i));
                        shouldRemove = true;
                        break;
                    case Kasra:
                        positions.Add(new TashkeelPosition(Kasra, i));
                        shouldRemove = true;
                        break;
                    case Shadda:
                        positions.Add(new TashkeelPosition(Shadda, i));
                        shouldRemove = true;
                        break;
                    case Sukun:
                        positions.Add(new TashkeelPosition(Sukun, i));
                        shouldRemove = true;
                        break;
                    case Maddah:
                        positions.Add(new TashkeelPosition(Maddah, i));
                        shouldRemove = true;
                        break;
                    case CombinedFathaShadda:
                    case CombinedDammaShadda:
                    case CombinedKasraShadda:
                        shouldRemove = true;
                        break;
                }

                if (shouldRemove)
                {
                    if (i > lastSplitIndex)
                    {
                        sb.Append(input, lastSplitIndex, i - lastSplitIndex);
                    }
                    lastSplitIndex = i + 1;
                }
            }

            if (lastSplitIndex < input.Length)
            {
                sb.Append(input, lastSplitIndex, input.Length - lastSplitIndex);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Restores tashkeel marks to their original positions in the character array.
        /// Modifies the array in-place and updates the length.
        /// </summary>
        public static void RestoreTashkeel(char[] letters, ref int length, List<TashkeelPosition> positions)
        {
            if (positions == null || positions.Count == 0)
                return;

            int newLength = length + positions.Count;
            
            // Shift characters to make room for tashkeel
            for (int i = length - 1; i >= 0; i--)
            {
                int newIndex = i + CountTashkeelBeforePosition(positions, i);
                if (newIndex < letters.Length)
                {
                    letters[newIndex] = letters[i];
                }
            }

            // Insert tashkeel marks
            foreach (var position in positions)
            {
                int insertIndex = position.Position;
                if (insertIndex < letters.Length)
                {
                    letters[insertIndex] = position.Tashkeel;
                }
            }

            length = newLength;
        }

        /// <summary>
        /// Counts how many tashkeel marks should appear before the given original position.
        /// </summary>
        private static int CountTashkeelBeforePosition(List<TashkeelPosition> positions, int originalPosition)
        {
            int count = 0;
            foreach (var pos in positions)
            {
                if (pos.Position <= originalPosition)
                    count++;
            }
            return count;
        }
    }
}
