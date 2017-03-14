namespace Prometheus.Services.Service
{
    public class VariableSnapshot
    {
        public string Type { get; set; }
        public string SnapshotVariable { get; set; }
        public string Variable { get; set; }

        public override string ToString()
        {
            return $"{Type} {SnapshotVariable} = {Variable}";
        }
    }
}