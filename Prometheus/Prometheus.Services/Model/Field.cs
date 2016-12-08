namespace Prometheus.Services.Model
{
    public class Field
    {
        public string Type { get; }
        public string Name { get; }

        public Field(string type, string name)
        {
            Type = type;
            Name = name;
        }

        public override bool Equals(object obj) {
            if ((obj == null) || GetType() != obj.GetType())
                return false;

            var field = (Field)obj;

            if (Name != field.Name)
                return false;

            if (Type != field.Type)
                return false;

            return true;
        }

        public override int GetHashCode() {
            return (Name+Type).GetHashCode();
        }
    }
}