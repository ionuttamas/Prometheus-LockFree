using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Services.Model;

namespace Prometheus.Services.Service
{
    //todo: currently this operates only on pointers (without values)
    public class Operation
    {
        public string Name { get; set; }
        public Structure Structure { get; set; }
        public Dictionary<string, string> Members { get; set; }

        public override string ToString()
        {
            string result = $"typedef struct {Name} {{ {Environment.NewLine} " +
                            Members.Select(x=>$"{x.Key} {x.Value};") +
                            $"}} {Name};";

            return result;
        }
    }
}