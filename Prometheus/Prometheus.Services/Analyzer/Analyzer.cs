using Antlr4.Runtime.Tree;
using Prometheus.Services.Model;

namespace Prometheus.Services
{
    public class Analyzer
    {
        private readonly DataStructureExtractor _extractor;

        public Analyzer()
        {
            _extractor = new DataStructureExtractor(new DataStructure());
        }

        /// <summary>
        /// The global state consists of all fields (primitive or structs) declared outside of the operations.
        /// </summary>
        public State GetState(IParseTree codeTree)
        {
            _extractor.Visit(codeTree);

            return _extractor.DataStructure.GlobalState;
        }
    }
}