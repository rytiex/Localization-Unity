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

        /// <summary>
        /// Determines the position of a letter within a word.
        /// </summary>
        public static LetterPosition GetLetterPosition(char[] word, int index)
        {
            bool hasPrevious = index > 0 && CanConnectFromPrevious(word, index);
            bool hasNext = index < word.Length - 1 && CanConnectToNext(word, index);

            if (hasPrevious && hasNext)
                return LetterPosition.Middle;
            if (hasPrevious)
                return LetterPosition.End;
            if (hasNext)
                return LetterPosition.Beginning;
            
            return LetterPosition.Isolated;
        }

        private static bool CanConnectFromPrevious(char[] word, int index)
        {
            if (index == 0) return false;
            
            char prevChar = word[index - 1];
            if (prevChar < 0xFE80 || prevChar > 0xFEFF)
                return false;
            
            return !IsNonConnecting(prevChar);
        }

        private static bool CanConnectToNext(char[] word, int index)
        {
            if (index >= word.Length - 1) return false;
            
            char nextChar = word[index + 1];
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
            bool isPresentationFormB = ch >= (char)0xFE70 && ch <= (char)0xFEFF;
            
            bool isPersianCharacter = ch is (char)0xFB56 or (char)0xFB7A or (char)0xFB8A or (char)0xFB92 or (char)0xFB8E;
            
            bool isAcceptableCharacter = isPresentationFormB || isPersianCharacter || ch == (char)0xFBFC;

            if (!isAcceptableCharacter)
                return true;

            if (char.IsPunctuation(ch) || char.IsSymbol(ch) || char.IsNumber(ch))
                return true;

            if (char.IsLower(ch) || char.IsUpper(ch))
                return true;

            return ch == 'a' || ch == '>' || ch == '<' || ch == (char)0x061B;
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
                    combinedLam = (char)0xFEF7; // Lam-Alef Maksora
                    nextOutput = (char)0xFFFF;
                    return true;
                case (char)IsolatedArabicLetters.Alef:
                    combinedLam = (char)0xFEF9; // Lam-Alef
                    nextOutput = (char)0xFFFF;
                    return true;
                case (char)IsolatedArabicLetters.AlefHamza:
                    combinedLam = (char)0xFEF5; // Lam-Alef Hamza
                    nextOutput = (char)0xFFFF;
                    return true;
                case (char)IsolatedArabicLetters.AlefMad:
                    combinedLam = (char)0xFEF3; // Lam-Alef Mad
                    nextOutput = (char)0xFFFF;
                    return true;
            }

            return false;
        }
    }
}
