using System;
using System.Linq;
using System.Text;
using Prometheus.Common;

namespace Prometheus.Services.Service
{
    public class InsertionDeclaration : IDeclaration
    {
        public int Index { get; set; }
        public string Value { get; set; }

        public InsertionDeclaration(int index, string value) {
            Index = index;
            Value = value;
        }

        public string ApplyOn(string text)
        {
            Value = Indent(text, Index);
            return text.InsertAt(Index, Value);
        }


        private string Indent(string text, int index) {
            var indentOffset = index - text.Substring(0, index).InvariantLastIndexOf(Environment.NewLine) - 2;
            var declarationStatements = Value
                .Split(Environment.NewLine)
                .ToList();
            string indentSpaces = indentOffset > 0 ? string.Join("", Enumerable.Repeat(" ", indentOffset)):string.Empty;
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