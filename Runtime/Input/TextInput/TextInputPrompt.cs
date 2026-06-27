namespace Sentinal.Input
{
    /// <summary>
    /// Configuration for a modal text entry prompt.
    /// </summary>
    public readonly struct TextInputPrompt
    {
        public string Header { get; }
        public string Placeholder { get; }
        public string InitialValue { get; }
        public bool Multiline { get; }
        public int MaxLength { get; }

        private TextInputPrompt(string header, string placeholder, string initialValue, bool multiline, int maxLength)
        {
            Header = header;
            Placeholder = placeholder;
            InitialValue = initialValue;
            Multiline = multiline;
            MaxLength = maxLength;
        }

        public static TextInputPrompt SingleLine(
            string header,
            string placeholder = "",
            string initialValue = "",
            int maxLength = 128
        ) => new(header, placeholder, initialValue, multiline: false, maxLength);

        public static TextInputPrompt MultiLine(
            string header,
            string placeholder = "",
            string initialValue = "",
            int maxLength = 2048
        ) => new(header, placeholder, initialValue, multiline: true, maxLength);
    }
}
