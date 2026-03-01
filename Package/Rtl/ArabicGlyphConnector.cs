namespace PicoShot.Localization.Rtl
{
    /// <summary>
    /// Determines the position type of an Arabic character in a word.
    /// </summary>
    internal enum LetterPosition
    {
        Isolated,
        Beginning,
        Middle,
        End
    }

    /// <summary>
    /// Handles connecting Arabic letters to form proper words.
    /// </summary>
    internal static class ArabicGlyphConnector
    {
        private static readonly bool[] NonConnectingLetters = new bool[256];

        static ArabicGlyphConnector()
        {
            MarkNonConnecting(IsolatedArabicLetters.Dal);
            MarkNonConnecting(IsolatedArabicLetters.Thal);
            MarkNonConnecting(IsolatedArabicLetters.Ra2);
            MarkNonConnecting(IsolatedArabicLetters.Zeen);
            MarkNonConnecting(IsolatedArabicLetters.PersianZe);
            MarkNonConnecting(IsolatedArabicLetters.Waw);
            MarkNonConnecting(IsolatedArabicLetters.Alef);
            MarkNonConnecting(IsolatedArabicLetters.AlefHamza);
            MarkNonConnecting(IsolatedArabicLetters.AlefMaksoor);
            MarkNonConnecting(IsolatedArabicLetters.AlefMad);
            MarkNonConnecting(IsolatedArabicLetters.WawHamza);
            MarkNonConnecting(IsolatedArabicLetters.Hamza);
        }

        private static void MarkNonConnecting(IsolatedArabicLetters letter)
        {
            int index = (int)letter - 0xFE80;
            if (index >= 0 && index < NonConnectingLetters.Length)
            {
                NonConnectingLetters[index] = true;
            }
        }

        private static bool IsNonConnecting(char isolatedForm)
        {
            int index = isolatedForm - 0xFE80;
            if (index >= 0 && index < NonConnectingLetters.Length)
            {
                return NonConnectingLetters[index];
            }
            return false;
        }

        private static bool IsTashkeel(char c)
        {
            return c is '\u064B' or '\u064C' or '\u064D' or '\u064E' or '\u064F' or '\u0650' or '\u0651' or '\u0652' or '\u0653';
        }

        /// <summary>
        /// Determines the position of a letter within a word.
        /// </summary>
        public static LetterPosition GetLetterPosition(char[] word, int index, int length)
        {
            bool hasPrevious = index > 0 && CanConnectFromPrevious(word, index);
            bool hasNext = index < length - 1 && CanConnectToNext(word, index);

            if (hasPrevious && hasNext)
                return LetterPosition.Middle;
            if (hasPrevious)
                return LetterPosition.End;
            if (hasNext)
                return LetterPosition.Beginning;

            return LetterPosition.Isolated;
        }

        /// <summary>
        /// Determines the position of a letter within a word (uses word.Length as length).
        /// </summary>
        public static LetterPosition GetLetterPosition(char[] word, int index)
        {
            return GetLetterPosition(word, index, word.Length);
        }

        private static bool CanConnectFromPrevious(char[] word, int index)
        {
            if (index == 0) return false;

            int prevIndex = index - 1;
            while (prevIndex >= 0 && IsTashkeel(word[prevIndex]))
                prevIndex--;

            if (prevIndex < 0) return false;

            char prevChar = word[prevIndex];
            if (prevChar < 0xFE80 || prevChar > 0xFEFF)
                return false;

            return !IsNonConnecting(prevChar);
        }

        private static bool CanConnectToNext(char[] word, int index)
        {
            if (index >= word.Length - 1) return false;

            int nextIndex = index + 1;
            while (nextIndex < word.Length && IsTashkeel(word[nextIndex]))
                nextIndex++;

            if (nextIndex >= word.Length) return false;

            char nextChar = word[nextIndex];
            if (nextChar < 0xFE80 || nextChar > 0xFEFF)
                return false;

            return !IsNonConnecting(word[index]);
        }

        /// <summary>
        /// Gets the final glyph form for a character based on its position in the word.
        /// </summary>
        public static char GetGlyphForm(char isolatedForm, LetterPosition position)
        {
            if (isolatedForm == (char)0xFFFF)
                return isolatedForm;

            return position switch
            {
                LetterPosition.Isolated => isolatedForm,
                LetterPosition.Beginning => (char)(isolatedForm + 2),
                LetterPosition.Middle => (char)(isolatedForm + 3),
                LetterPosition.End => (char)(isolatedForm + 1),
                _ => isolatedForm
            };
        }

        /// <summary>
        /// Checks if a character is an ignored/special character that doesn't participate in Arabic shaping.
        /// </summary>
        public static bool IsIgnoredCharacter(char ch)
        {
            bool isPresentationFormB = ch >= '\uFE70' && ch <= '\uFEFF';

            bool isPersianCharacter = ch is '\uFB56' or '\uFB7A' or '\uFB8A' or '\uFB92' or '\uFB8E';

            bool isAcceptableCharacter = isPresentationFormB || isPersianCharacter || ch == '\uFBFC';

            if (!isAcceptableCharacter)
                return true;

            if (char.IsPunctuation(ch) || char.IsSymbol(ch) || char.IsNumber(ch))
                return true;

            if (char.IsLower(ch) || char.IsUpper(ch))
                return true;

            return ch == 'a' || ch == '>' || ch == '<' || ch == '\u061B';
        }

        /// <summary>
        /// Handles the special Lam-Alef ligature combination.
        /// </summary>
        public static bool TryCombineLamAlef(char lam, char nextChar, out char combinedLam, out char nextOutput)
        {
            combinedLam = lam;
            nextOutput = nextChar;

            if (lam != (char)IsolatedArabicLetters.Lam)
                return false;

            switch (nextChar)
            {
                case (char)IsolatedArabicLetters.AlefMaksoor:
                    combinedLam = '\uFEF7'; // Lam-Alef Maksora
                    nextOutput = (char)0xFFFF;
                    return true;
                case (char)IsolatedArabicLetters.Alef:
                    combinedLam = '\uFEF9'; // Lam-Alef
                    nextOutput = (char)0xFFFF;
                    return true;
                case (char)IsolatedArabicLetters.AlefHamza:
                    combinedLam = '\uFEF5'; // Lam-Alef Hamza
                    nextOutput = (char)0xFFFF;
                    return true;
                case (char)IsolatedArabicLetters.AlefMad:
                    combinedLam = '\uFEF3'; // Lam-Alef Mad
                    nextOutput = (char)0xFFFF;
                    return true;
            }

            return false;
        }
    }
}
