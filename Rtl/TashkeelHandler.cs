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
    /// </summary>
    internal static class TashkeelHandler
    {
        private static readonly StringBuilder InternalBuilder = new(1024);
        private static readonly List<TashkeelPosition> TashkeelPositions = new(64);

        private const char TanweenFatha = (char)0x064B;
        private const char TanweenDamma = (char)0x064C;
        private const char TanweenKasra = (char)0x064D;
        private const char Fatha = (char)0x064E;
        private const char Damma = (char)0x064F;
        private const char Kasra = (char)0x0650;
        private const char Shadda = (char)0x0651;
        private const char Sukun = (char)0x0652;
        private const char Maddah = (char)0x0653;

        private const char CombinedFathaShadda = (char)0xFC60;
        private const char CombinedDammaShadda = (char)0xFC61;
        private const char CombinedKasraShadda = (char)0xFC62;

        /// <summary>
        /// Removes tashkeel from the string and stores positions for later restoration.
        /// </summary>
        public static string RemoveTashkeel(string input, out List<TashkeelPosition> positions)
        {
            positions = new List<TashkeelPosition>(input.Length / 4);
            InternalBuilder.Clear();
            InternalBuilder.EnsureCapacity(input.Length);

            int lastSplitIndex = 0;
            int tashkeelIndex = 0;

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
                        InternalBuilder.Append(input, lastSplitIndex, i - lastSplitIndex);
                    }
                    lastSplitIndex = i + 1;

                    if (currentChar != CombinedFathaShadda && 
                        currentChar != CombinedDammaShadda && 
                        currentChar != CombinedKasraShadda)
                    {
                        tashkeelIndex++;
                    }
                }
            }

            if (lastSplitIndex < input.Length)
            {
                InternalBuilder.Append(input, lastSplitIndex, input.Length - lastSplitIndex);
            }

            return InternalBuilder.ToString();
        }

        /// <summary>
        /// Restores tashkeel marks to their original positions in the character array.
        /// </summary>
        public static void RestoreTashkeel(ref char[] letters, List<TashkeelPosition> positions)
        {
            if (positions == null || positions.Count == 0)
                return;

            System.Array.Resize(ref letters, letters.Length + positions.Count);

            foreach (var position in positions)
            {
                for (int j = letters.Length - 1; j > position.Position; j--)
                {
                    letters[j] = letters[j - 1];
                }
                letters[position.Position] = position.Tashkeel;
            }
        }
    }
}
