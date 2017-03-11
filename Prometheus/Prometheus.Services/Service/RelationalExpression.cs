namespace Prometheus.Services.Service
{
    public class RelationalExpression {
        public string LeftOperand { get; set; }
        public string RightOperand { get; set; }
        public Interval LeftOperandInterval { get; set; }
        public Interval RightOperandInterval { get; set; }
        public string Method { get; set; }
    }
}