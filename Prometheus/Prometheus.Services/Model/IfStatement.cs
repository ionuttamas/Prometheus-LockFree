using System.Collections.Generic;
using Prometheus.Services.Parser;

namespace Prometheus.Services.Model
{
    public class IfStatement
    {
        public IfStatement() {
            IfStatements = new List<IfStatement>();
        }

        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public List<IfStatement> IfStatements { get; set; }
        public List<CLanguageParser.AssignmentExpressionContext> Assignments { get; set; }
        public List<ElseStatement> ElseStatements { get; set; }
    }

    public class ElseStatement {
        public int Index { get; set; }
        public List<CLanguageParser.AssignmentExpressionContext> Assignments { get; set; }
    }
}