namespace PicoShot.Localization
{
    public static class Extensions
    {
        public static string Localized(this string key, params object[] args)
        {
            return LocalizationManager.GetText(key, args);
        }

        public static string[] LocalizedArray(this string key)
        {
            return LocalizationManager.GetArray(key);
        }

        public static string LocalizedArrayElement(this string key, int index)
        {
            return LocalizationManager.GetArrayText(key, index);
        }
    }
}
