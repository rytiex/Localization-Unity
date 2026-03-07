namespace PicoShot.Localization
{
    public readonly struct Key
    {
        public readonly string Value;

        private Key(string value)
        {
            Value = value;
        }

        public static Key From(string key) => new(key);

        public static implicit operator string(Key key) => key.Value;

        public static implicit operator Key(string value) => new(value);

    }
}