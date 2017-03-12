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
            return text.InsertAt(Index, Value);
        }
    }
}