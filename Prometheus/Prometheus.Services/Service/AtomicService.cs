using System.Collections.Generic;
using System.Linq;
using Prometheus.Services.Model;

namespace Prometheus.Services.Service
{
    public class AtomicService
    {
        private const string UNMARKED_POINTER = "STATE_OP_NONE";

        public string GetUnmarkedPointerDefinition()
        {
            return $"#define {UNMARKED_POINTER} 0";
        }

        public string GetFlagRetrievalStatement(Structure structure)
        {
            return $"static inline uint64_t GETFLAG({structure.Name}* ptr) {{ " +
                   "return ((uint64_t)ptr) & 8; " +
                   "}";
        }

        public string GetFlagStatement(Structure structure)
        {
            return $"static inline uint64_t FLAG({structure.Name}* ptr, uint64_t flag) {{ " +
                   "return ((uint64_t)ptr) & 8; " +
                   "}";
        }

        public string GetCheckFlagCondition(List<string> variables)
        {
            var variableCondition = $"GETFLAG({{0}})!={UNMARKED_POINTER}";
            var condition = string.Join("||", variables.Select(x => string.Format(variableCondition, (object) x)));
            var checkExpression = $"if({condition}) {{ HELP HERE }}";

            return checkExpression;
        }
    }
}