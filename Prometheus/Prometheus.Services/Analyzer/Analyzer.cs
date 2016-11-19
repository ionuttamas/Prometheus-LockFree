using Antlr4.Runtime.Tree;
using Prometheus.Services.Parser;
using Prometheus.Domain;
using Prometheus.Services.Model;

namespace Prometheus.Services
{
    public class Analyzer
    {
        private readonly CodeVisitor _visitor;

        public Analyzer()
        {
            _visitor = new CodeVisitor(new DataStructure());
        }

        /// <summary>
        /// The global state consists of all fields (primitive or structs) declared outside of the operations.
        /// </summary>
        public State GetState(IParseTree codeTree)
        {
            _visitor.Visit(codeTree);

            return _visitor.DataStructure.GlobalState;
        }
    }
}