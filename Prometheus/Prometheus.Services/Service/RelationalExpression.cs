using System.Collections.Generic;

namespace Prometheus.Services.Service
{
    public class RelationalExpression {
        public string LeftOperand { get; set; }
        public string RightOperand { get; set; }
        public KeyValuePair<int, int> LeftOperandInterval { get; set; }
        public KeyValuePair<int, int> RightOperandInterval { get; set; }
        public string Method { get; set; }
    }
}