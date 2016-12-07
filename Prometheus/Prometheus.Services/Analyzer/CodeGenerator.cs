using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime.Tree;
using Prometheus.Services.Extensions;
using Prometheus.Services.Parser;

namespace Prometheus.Services
{
    public class CodeGenerator : CodeVisitor
    {
        private readonly StringBuilder _builder;

        public CodeGenerator()
        {
            _builder = new StringBuilder();
        }

        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context)
        {
            IEnumerable<RelationalExpression> relations = context
                .expression()
                .GetLeafDescendants(x => x is CLanguageParser.EqualityExpressionContext)
                .Select(x=>(CLanguageParser.EqualityExpressionContext)x.Parent)
                .Select(GetRelationalExpression);

            Console.WriteLine(string.Join(",", relations.Select(x=>x.LeftOperand +" and "+x.RightOperand)));
            return base.VisitSelectionStatement(context);
        }

        public override void PreVisit(IParseTree tree, string input)
        {
        }

        public override void PostVisit(IParseTree tree, string input)
        {
        }

        private RelationalExpression GetRelationalExpression(CLanguageParser.EqualityExpressionContext context)
        {
            var result = new RelationalExpression
            {
                LeftOperand = context.equalityExpression().relationalExpression().GetText(),
                RightOperand = context.relationalExpression().GetText()
            };

            return result;
        }

        private class RelationalExpression
        {
            public string LeftOperand { get; set; }
            public string RightOperand { get; set; }
        }
    }
}