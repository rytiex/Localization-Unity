namespace PicoShot.Localization.Rtl
{
    /// <summary>
    /// Arabic/Persian letters in their isolated Unicode form.
    /// </summary>
    internal enum IsolatedArabicLetters
    {
        Hamza = 0xFE80,
        Alef = 0xFE8D,
        AlefHamza = 0xFE83,
        WawHamza = 0xFE85,
        AlefMaksoor = 0xFE87,
        AlefMaksora = 0xFBFC,
        HamzaNabera = 0xFE89,
        Ba = 0xFE8F,
        Ta = 0xFE95,
        Tha2 = 0xFE99,
        Jeem = 0xFE9D,
        H7AA = 0xFEA1,
        Khaa2 = 0xFEA5,
        Dal = 0xFEA9,
        Thal = 0xFEAB,
        Ra2 = 0xFEAD,
        Zeen = 0xFEAF,
        Seen = 0xFEB1,
        Sheen = 0xFEB5,
        S9A = 0xFEB9,
        Dha = 0xFEBD,
        T6A = 0xFEC1,
        T6Ha = 0xFEC5,
        Ain = 0xFEC9,
        Gain = 0xFECD,
        Fa = 0xFED1,
        Gaf = 0xFED5,
        Kaf = 0xFED9,
        Lam = 0xFEDD,
        Meem = 0xFEE1,
        Noon = 0xFEE5,
        Ha = 0xFEE9,
        Waw = 0xFEED,
        Ya = 0xFEF1,
        AlefMad = 0xFE81,
        TaMarboota = 0xFE93,
        PersianPe = 0xFB56,
        PersianChe = 0xFB7A,
        PersianZe = 0xFB8A,
        PersianGaf = 0xFB92,
        PersianGaf2 = 0xFB8E,
        PersianYeh = 0xFBFC,
    }

    /// <summary>
    /// Arabic/Persian letters in their general Unicode form.
    /// </summary>
    internal enum GeneralArabicLetters
    {
        Hamza = 0x0621,
        Alef = 0x0627,
        AlefHamza = 0x0623,
        WawHamza = 0x0624,
        AlefMaksoor = 0x0625,
        AlefMagsora = 0x0649,
        HamzaNabera = 0x0626,
        Ba = 0x0628,
        Ta = 0x062A,
        Tha2 = 0x062B,
        Jeem = 0x062C,
        H7AA = 0x062D,
        Khaa2 = 0x062E,
        Dal = 0x062F,
        Thal = 0x0630,
        Ra2 = 0x0631,
        Zeen = 0x0632,
        Seen = 0x0633,
        Sheen = 0x0634,
        S9A = 0x0635,
        Dha = 0x0636,
        T6A = 0x0637,
        T6Ha = 0x0638,
        Ain = 0x0639,
        Gain = 0x063A,
        Fa = 0x0641,
        Gaf = 0x0642,
        Kaf = 0x0643,
        Lam = 0x0644,
        Meem = 0x0645,
        Noon = 0x0646,
        Ha = 0x0647,
        Waw = 0x0648,
        Ya = 0x064A,
        AlefMad = 0x0622,
        TaMarboota = 0x0629,
        PersianPe = 0x067E,
        PersianChe = 0x0686,
        PersianZe = 0x0698,
        PersianGaf = 0x06AF,
        PersianGaf2 = 0x06A9,
        PersianYeh = 0x06CC,
    }

    /// <summary>
    /// Provides conversion between general and isolated Arabic letter forms.
    /// </summary>
    internal static class ArabicLetterConverter
    {
        private static readonly int[] GeneralToIsolated = new int[256];
        private static readonly bool _isInitialized;

        static ArabicLetterConverter()
        {
            // Initialize mapping array for fast lookup
            // Map: (GeneralLetter - 0x0600) -> IsolatedLetter
            for (int i = 0; i < GeneralToIsolated.Length; i++)
            {
                GeneralToIsolated[i] = i + 0x0600; // Default to same value
            }

            AddMapping(GeneralArabicLetters.Hamza, IsolatedArabicLetters.Hamza);
            AddMapping(GeneralArabicLetters.Alef, IsolatedArabicLetters.Alef);
            AddMapping(GeneralArabicLetters.AlefHamza, IsolatedArabicLetters.AlefHamza);
            AddMapping(GeneralArabicLetters.WawHamza, IsolatedArabicLetters.WawHamza);
            AddMapping(GeneralArabicLetters.AlefMaksoor, IsolatedArabicLetters.AlefMaksoor);
            AddMapping(GeneralArabicLetters.AlefMagsora, IsolatedArabicLetters.AlefMaksora);
            AddMapping(GeneralArabicLetters.HamzaNabera, IsolatedArabicLetters.HamzaNabera);
            AddMapping(GeneralArabicLetters.Ba, IsolatedArabicLetters.Ba);
            AddMapping(GeneralArabicLetters.Ta, IsolatedArabicLetters.Ta);
            AddMapping(GeneralArabicLetters.Tha2, IsolatedArabicLetters.Tha2);
            AddMapping(GeneralArabicLetters.Jeem, IsolatedArabicLetters.Jeem);
            AddMapping(GeneralArabicLetters.H7AA, IsolatedArabicLetters.H7AA);
            AddMapping(GeneralArabicLetters.Khaa2, IsolatedArabicLetters.Khaa2);
            AddMapping(GeneralArabicLetters.Dal, IsolatedArabicLetters.Dal);
            AddMapping(GeneralArabicLetters.Thal, IsolatedArabicLetters.Thal);
            AddMapping(GeneralArabicLetters.Ra2, IsolatedArabicLetters.Ra2);
            AddMapping(GeneralArabicLetters.Zeen, IsolatedArabicLetters.Zeen);
            AddMapping(GeneralArabicLetters.Seen, IsolatedArabicLetters.Seen);
            AddMapping(GeneralArabicLetters.Sheen, IsolatedArabicLetters.Sheen);
            AddMapping(GeneralArabicLetters.S9A, IsolatedArabicLetters.S9A);
            AddMapping(GeneralArabicLetters.Dha, IsolatedArabicLetters.Dha);
            AddMapping(GeneralArabicLetters.T6A, IsolatedArabicLetters.T6A);
            AddMapping(GeneralArabicLetters.T6Ha, IsolatedArabicLetters.T6Ha);
            AddMapping(GeneralArabicLetters.Ain, IsolatedArabicLetters.Ain);
            AddMapping(GeneralArabicLetters.Gain, IsolatedArabicLetters.Gain);
            AddMapping(GeneralArabicLetters.Fa, IsolatedArabicLetters.Fa);
            AddMapping(GeneralArabicLetters.Gaf, IsolatedArabicLetters.Gaf);
            AddMapping(GeneralArabicLetters.Kaf, IsolatedArabicLetters.Kaf);
            AddMapping(GeneralArabicLetters.Lam, IsolatedArabicLetters.Lam);
            AddMapping(GeneralArabicLetters.Meem, IsolatedArabicLetters.Meem);
            AddMapping(GeneralArabicLetters.Noon, IsolatedArabicLetters.Noon);
            AddMapping(GeneralArabicLetters.Ha, IsolatedArabicLetters.Ha);
            AddMapping(GeneralArabicLetters.Waw, IsolatedArabicLetters.Waw);
            AddMapping(GeneralArabicLetters.Ya, IsolatedArabicLetters.Ya);
            AddMapping(GeneralArabicLetters.AlefMad, IsolatedArabicLetters.AlefMad);
            AddMapping(GeneralArabicLetters.TaMarboota, IsolatedArabicLetters.TaMarboota);
            AddMapping(GeneralArabicLetters.PersianPe, IsolatedArabicLetters.PersianPe);
            AddMapping(GeneralArabicLetters.PersianChe, IsolatedArabicLetters.PersianChe);
            AddMapping(GeneralArabicLetters.PersianZe, IsolatedArabicLetters.PersianZe);
            AddMapping(GeneralArabicLetters.PersianGaf, IsolatedArabicLetters.PersianGaf);
            AddMapping(GeneralArabicLetters.PersianGaf2, IsolatedArabicLetters.PersianGaf2);
            AddMapping(GeneralArabicLetters.PersianYeh, IsolatedArabicLetters.PersianYeh);

            _isInitialized = true;
        }

        private static void AddMapping(GeneralArabicLetters general, IsolatedArabicLetters isolated)
        {
            int index = (int)general - 0x0600;
            if (index >= 0 && index < GeneralToIsolated.Length)
            {
                GeneralToIsolated[index] = (int)isolated;
            }
        }

        /// <summary>
        /// Converts a general Arabic character to its isolated form.
        /// </summary>
        public static int ToIsolated(int charCode)
        {
            int index = charCode - 0x0600;
            if (index >= 0 && index < GeneralToIsolated.Length)
            {
                return GeneralToIsolated[index];
            }
            return charCode;
        }
    }
}
