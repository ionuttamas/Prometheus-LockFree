using System;
using System.Linq;
using System.Text;
using Prometheus.Common;

namespace Prometheus.Services.Service
{
    public class ReplacementDeclaration : IDeclaration
    {
        public int Index => From;
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
            var previousText = text.Substring(text.Substring(0, From).InvariantLastIndexOf(Environment.NewLine), Index- text.Substring(0, From).InvariantLastIndexOf(Environment.NewLine));
            if (string.IsNullOrWhiteSpace(previousText))
            {
                Value = Indent(text, From);
            }

            text = $"{text.Substring(0, From)}{Value}{text.Substring(To + 1)}";
            return text;
        }

        private string Indent(string text, int index) {
            var indentOffset = index - text.Substring(0, index).InvariantLastIndexOf(Environment.NewLine) - 2;
            var declarationStatements = Value
                .Split(Environment.NewLine)
                .ToList();
            string indentSpaces = indentOffset > 0 ? string.Join("", Enumerable.Repeat(" ", indentOffset)) : string.Empty;
            var builder = new StringBuilder();
            builder.AppendLine(declarationStatements[0]);

            foreach (var declaration in Value.Split(Environment.NewLine).Skip(1)) {
                builder.AppendLine(indentSpaces + declaration);
            }

            builder.Append(indentSpaces);

            return builder.ToString();
        }
    }
}