using System.Collections.Generic;
using System.Linq;
using Prometheus.Services.Parser;

namespace Prometheus.Services.Model
{
    public class Operation
    {
        public string Name { get; }
        public List<Variable> LocalVariables { get; }
        public List<IfStatement> IfStatements { get; }

        public Operation(string name)
        {
            Name = name;
            IfStatements = new List<IfStatement>();
            LocalVariables = new List<Variable>();
        }

        public Operation(string name, List<Variable> variables)
            : this(name)
        {
            LocalVariables = variables;
        }

        public void AddVariable(string name, string type, List<string> dependantVariables)
        {
            Variable variable = this[name];

            if (variable == null)
            {
                var localVariable = new Variable(name, type, Name)
                {
                    DependentVariables = new HashSet<string>(dependantVariables)
                };
                LocalVariables.Add(localVariable);
            }
            else
            {
                variable.DependentVariables.UnionWith(dependantVariables);
            }
        }

        public Variable this[string name]
        {
            get { return LocalVariables.FirstOrDefault(x => x.Name == name); }
        }

        public override bool Equals(object obj)
        {
            if ((obj == null) || GetType() != obj.GetType())
                return false;

            var operation = (Operation)obj;

            if (Name != operation.Name)
                return false;

            if (LocalVariables == null || operation.LocalVariables == null)
                return false;

            if (LocalVariables.Count != operation.LocalVariables.Count)
                return false;

            if (LocalVariables.Any(x => !operation.LocalVariables.Contains(x)))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}