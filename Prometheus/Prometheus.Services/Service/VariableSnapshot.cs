namespace Prometheus.Services.Service
{
    public class VariableSnapshot
    {
        public string Type { get; }
        public string SnapshotVariable { get; }
        public string Variable { get; }

        public VariableSnapshot(string type, string snapshot, string variable)
        {
            Type = type;
            SnapshotVariable = snapshot;
            Variable = variable;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || GetType() != obj.GetType())
                return false;

            var field = (VariableSnapshot)obj;

            if (Variable != field.Variable)
                return false;

            return true;
        }

        public override int GetHashCode() {
            return Variable.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Type} {SnapshotVariable} = {Variable}";
        }
    }
}