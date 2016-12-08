using System;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Prometheus.Services.Parser;

namespace Prometheus.Services
{
    public abstract class CodeVisitor : CLanguageBaseVisitor<object>
    {
        private AntlrInputStream _inputStream;
        private CLanguageLexer _lexer;
        private CommonTokenStream _tokenStream;
        private CLanguageParser _parser;

        public void Visit(string input)
        {
            _inputStream = new AntlrInputStream(input);
            _lexer = new CLanguageLexer(_inputStream);
            _tokenStream = new CommonTokenStream(_lexer);
            _parser = new CLanguageParser(_tokenStream);
            var tree = _parser.compilationUnit();
            Console.WriteLine(tree.ToStringTree(_parser));
            PreVisit(tree, input);
            Visit(tree);
            PostVisit(tree, input);
        }

        public abstract void PreVisit(IParseTree tree, string input);
        public abstract void PostVisit(IParseTree tree, string input);
    }
}
