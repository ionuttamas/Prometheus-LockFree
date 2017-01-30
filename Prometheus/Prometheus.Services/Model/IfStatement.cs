using System.Collections.Generic;
using Prometheus.Services.Extensions;
using Prometheus.Services.Parser;

namespace Prometheus.Services.Model
{
    public class IfStatement
    {
        public IfStatement() {
            IfStatements = new List<IfStatement>();
        }

        public CLanguageParser.SelectionStatementContext Context { get; set; }
        public int StartIndex => Context.GetStartIndex();
        public int EndIndex => Context.GetStopIndex();
        public List<IfStatement> IfStatements { get; set; }
        public List<CLanguageParser.AssignmentExpressionContext> Assignments { get; set; }
        public List<ElseStatement> ElseStatements { get; set; }
    }

    public class ElseStatement {
        public CLanguageParser.SelectionStatementContext Context { get; set; } //todo
        public int StartIndex => Context.GetStartIndex();
        public int EndIndex => Context.GetStopIndex();
        public List<IfStatement> IfStatements { get; set; }
        public List<CLanguageParser.AssignmentExpressionContext> Assignments { get; set; }
    }
}