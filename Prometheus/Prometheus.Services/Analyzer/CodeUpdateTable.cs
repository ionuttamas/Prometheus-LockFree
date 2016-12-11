using System.Collections.Generic;

namespace Prometheus.Services
{
    public class CodeUpdateTable {
        public Dictionary<int, string> Updates { get; }

        public CodeUpdateTable() {
            Updates = new Dictionary<int, string>();
        }

        public void Add(int index, string code) {
            Updates[index] = code;
        }
    }
}