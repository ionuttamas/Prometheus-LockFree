using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Services.Model
{
    public class Variable
    {
        public string Name { get; }
        public string Type { get; }
        public string Operation { get; } //TODO: check if needed
        public int Index { get; set; }
        public bool LinksToGlobalState { get; set; }
        public HashSet<string> DependentVariables { get; set; }

        public Variable(string name, string type, string operation)
        {
            Name = name;
            Operation = operation;
            Type = type;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || GetType() != obj.GetType())
                return false;

            var variable = (Variable)obj;

            if (Name != variable.Name)
                return false;

            if (Type != variable.Type)
                return false;

            if (LinksToGlobalState != variable.LinksToGlobalState)
                return false;

            if (DependentVariables == null || variable.DependentVariables == null)
                return false;

            if (DependentVariables.Count != variable.DependentVariables.Count)
                return false;

            if (DependentVariables.Any(x => !variable.DependentVariables.Contains(x)))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}