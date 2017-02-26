using System.Collections.Generic;
using System.Linq;
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

        public void AddIfStatement(IfStatement statement) {
            var parentIfStatement = IfStatements
                .FirstOrDefault(x => x.StartIndex < statement.StartIndex &&
                                     statement.EndIndex < x.EndIndex);

            if (parentIfStatement != null) {
                parentIfStatement.AddIfStatement(statement);
                return;
            }

            var parentElseStatement = ElseStatements
                .FirstOrDefault(x => x.StartIndex < statement.StartIndex &&
                                     statement.EndIndex < x.EndIndex);

            if (parentElseStatement != null) {
                parentElseStatement.AddIfStatement(statement);
                return;
            }

            IfStatements.Add(statement);
        }
    }

    public class ElseStatement {
        public CLanguageParser.StatementContext Context { get; set; }
        public int StartIndex => Context.GetStartIndex();
        public int EndIndex => Context.GetStopIndex();
        public List<IfStatement> IfStatements { get; set; }
        public List<CLanguageParser.AssignmentExpressionContext> Assignments { get; set; }

        public ElseStatement() {
            IfStatements = new List<IfStatement>();
        }

        public void AddIfStatement(IfStatement statement) {
            var parentIfStatement = IfStatements
                .FirstOrDefault(x => x.StartIndex < statement.StartIndex &&
                                     statement.EndIndex < x.EndIndex);

            if (parentIfStatement != null) {
                parentIfStatement.AddIfStatement(statement);
                return;
            }

            IfStatements.Add(statement);
        }
    }
}