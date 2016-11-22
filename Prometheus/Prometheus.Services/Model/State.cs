using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Services.Model
{
    public class State
    {
        public List<Variable> Variables { get; }

        public State()
        {
            Variables = new List<Variable>();
        }

        public Variable this[string name]
        {
            get { return Variables.FirstOrDefault(x => x.Name == name); }
        }

        public void Add(Variable variable)
        {
            Variables.Add(variable);
        }

        public bool Contains(string name)
        {
            return this[name] != null;
        }
    }
}