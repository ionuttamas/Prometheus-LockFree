using System.Collections.Generic;

namespace Prometheus.Services.Model
{
    public class State
    {
        public List<string> Variables { get;}

        public State()
        {
            Variables = new List<string>();
        }

        public void Add(string name)
        {
            Variables.Add(name);
        }

        public bool Contains(string name)
        {
            return Variables.Contains(name);
        }
    }
}