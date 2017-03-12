namespace Prometheus.Services.Service
{
    public class ReplacementDeclaration : IDeclaration {
        public int From { get; set; }
        public int To { get; set; }
        public string Value { get; set; }

        public ReplacementDeclaration(int from, int to, string value)
        {
            From = from;
            To = to;
            Value = value;
        }

        public string ApplyOn(string text)
        {
            text = $"{text.Substring(0, From)}{Value}{text.Substring(To + 1)}";
            return text;
        }
    }
}