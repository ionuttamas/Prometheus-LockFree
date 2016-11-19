using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Services.Model
{
    public class Variable
    {
        public string Name { get; }
        public string Operation { get; }
        public bool LinksToGlobalState { get; set; }
        public HashSet<string> DependentVariables { get; set; }

        public Variable(string name, string operation)
        {
            Name = name;
            Operation = operation;
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || GetType() != obj.GetType())
                return false;

            var variable = (Variable)obj;

            if (Name != variable.Name)
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