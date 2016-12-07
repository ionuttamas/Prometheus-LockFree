using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Services.Model
{
    public class Structure
    {
        public string Name { get; set; }
        public List<Field> Fields { get; set; }

        public Structure()
        {
            Fields = new List<Field>();
        }

        public Field this[string name]
        {
            get { return Fields.FirstOrDefault(x => x.Name == name); }
        }
    }
}