using System.Collections.Generic;
using System.Linq;
using Prometheus.Services.Extensions;
using Prometheus.Services.Parser;

namespace Prometheus.Services.Model
{
    public class Structure
    {
        public int StartIndex => Context.GetStartIndex();
        public int EndIndex => Context.GetStopIndex();
        public CLanguageParser.StructOrUnionSpecifierContext Context { get; set; }
        public string Name { get; }
        public List<Field> Fields { get; set; }

        public Structure(string name)
        {
            Name = name;
            Fields = new List<Field>();
        }

        public Field this[string name]
        {
            get { return Fields.FirstOrDefault(x => x.Name == name); }
        }

        public override bool Equals(object obj) {
            if ((obj == null) || GetType() != obj.GetType())
                return false;

            var structure = (Structure)obj;

            if (Name != structure.Name)
                return false;

            if (Fields == null || structure.Fields == null)
                return false;

            if (Fields.Count != structure.Fields.Count)
                return false;

            if (!Fields.Any(x => structure.Fields.Contains(x)))
                return false;

            return true;
        }

        public override int GetHashCode() {
            return Name.GetHashCode();
        }
    }
}