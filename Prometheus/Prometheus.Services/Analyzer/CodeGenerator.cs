using System.Text;
using Antlr4.Runtime.Tree;

namespace Prometheus.Services
{
    public class CodeGenerator : CodeVisitor
    {
        private readonly StringBuilder _builder;

        public CodeGenerator()
        {
            _builder = new StringBuilder();
        }

        public override void PreVisit(IParseTree tree, string input)
        {
        }

        public override void PostVisit(IParseTree tree, string input)
        {
        }
    }
}