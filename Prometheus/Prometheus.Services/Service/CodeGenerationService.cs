using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Prometheus.Common;
using Prometheus.Services.Extensions;
using Prometheus.Services.Model;
using Prometheus.Services.Parser;

namespace Prometheus.Services.Service {
    public class CodeGenerationService
    {
        private const string POINTER_ACCESS_MARKER = "->";
        private const string SNAPSHOT_NAME_MARKER = "old";
        private readonly DataStructure _dataStructure;
        private readonly TypeService _typeService;
        private readonly Dictionary<Type, Func<object, RelationalExpression>> _relationExtractors;

        public CodeGenerationService(DataStructure dataStructure, TypeService typeService)
        {
            _dataStructure = dataStructure;
            _typeService = typeService;
            _relationExtractors = new Dictionary<Type, Func<object, RelationalExpression>>
            {
                { typeof(CLanguageParser.EqualityExpressionContext), x => GetRelationalExpression((CLanguageParser.EqualityExpressionContext) x)},
                { typeof(CLanguageParser.AndExpressionContext), x => GetRelationalExpression((CLanguageParser.AndExpressionContext) x)}
            };
        }

        public Dictionary<int, string> GetWhileLoopDeclarations(CLanguageParser.FunctionDefinitionContext context)
        {
            int insertionIndex = int.MaxValue;
            string operationName = context.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName();

            var localVariables = _dataStructure[operationName]
                .LocalVariables
                .OrderBy(x => x.Index)
                .ToList();
            var variable = localVariables
                .Skip(1)
                .Select((var, ix) => new {Variable = var, Index = ix})
                .FirstOrDefault(x => x.Variable.LinksToGlobalState);

            if (variable != null)
            {
                insertionIndex = localVariables[variable.Index].Index;
            }

            var bodyContext = context.compoundStatement();
            string body = bodyContext.GetContextText();
            var globalVariableIndexes = _dataStructure
                .GlobalState
                .Variables
                .Select(x => new Regex($"[^a-zA-Z\\d:]{x.Name}[^a-zA-Z\\d:]"))
                .Select(x => x.IsMatch(body) ? x.Match(body).Index : -1)
                .Where(x => x > 0)
                .Select(x => x + bodyContext.Start.StartIndex);
            insertionIndex = Math.Min(insertionIndex, globalVariableIndexes.Any() ? globalVariableIndexes.Min() : insertionIndex);
            Operation operation = _dataStructure[operationName];
            IfStatement ifStatement = operation.IfStatements.FirstOrDefault(x => x.StartIndex < insertionIndex && insertionIndex < x.EndIndex);

            if (ifStatement != null)
            {
                insertionIndex = ifStatement.StartIndex;
            }

            insertionIndex = body.Substring(0, insertionIndex - bodyContext.Start.StartIndex).InvariantLastIndexOf(Environment.NewLine)+ bodyContext.Start.StartIndex+2;

            var result = new Dictionary<int, string>
            {
                {insertionIndex, "while (true) {"},
                {context.Stop.StopIndex - 1, "}"},
            };

            return result;
        }

        public KeyValuePair<int, string> GetSnapshotDeclarations(CLanguageParser.SelectionStatementContext context)
        {
            int index = context.Start.StartIndex;
            List<RelationalExpression> relationalExpressions = GetRelations(context)
                .Concat(ExtractAssignments(context))
                .ToList();
            var builder = new StringBuilder();

            foreach (var relationalExpression in relationalExpressions)
            {
                string declaration;
                if (GetSnapshotDeclaration(relationalExpression.LeftOperand, relationalExpression.Operation, out declaration))
                {
                    builder.AppendLine(declaration);
                }

                if (GetSnapshotDeclaration(relationalExpression.RightOperand, relationalExpression.Operation, out declaration))
                {
                    builder.AppendLine(declaration);
                }
            }

            return new KeyValuePair<int, string>(index, builder.ToString());
        }

        public List<Tuple<int, int, string>> GetReplacementDeclarations(CLanguageParser.SelectionStatementContext context)
        {
            var result = new List<Tuple<int, int, string>>();
            List<RelationalExpression> relationalExpressions = context
                .expression()
                .GetLeafDescendants(x => x is CLanguageParser.EqualityExpressionContext)
                .Select(x => (object)x.Parent)
                .Select(GetRelationalExpression)
                .ToList();

            foreach (var relationalExpression in relationalExpressions) {
                string declaration;
                int offset;
                if (GetReplacementDeclaration(relationalExpression.LeftOperand, relationalExpression.Operation, out declaration, out offset)) {
                    result.Add(Tuple.Create(relationalExpression.LeftOperandInterval.Key, relationalExpression.LeftOperandInterval.Value - offset, declaration));
                }

                if (GetReplacementDeclaration(relationalExpression.RightOperand, relationalExpression.Operation, out declaration, out offset)) {
                    result.Add(Tuple.Create(relationalExpression.RightOperandInterval.Key, relationalExpression.RightOperandInterval.Value - offset, declaration));
                }
            }

            return result;
        }

        private bool GetSnapshotDeclaration(string expression, string operation, out string declaration)
        {
            string variable = expression.Contains(POINTER_ACCESS_MARKER) ?
                expression.Split(POINTER_ACCESS_MARKER).First() :
                expression;

            if (_dataStructure.GlobalState.Contains(variable) || (_dataStructure[operation][variable]!=null && _dataStructure[operation][variable].LinksToGlobalState))
            {
                string type = _typeService.GetType(expression, operation);

                if (_typeService.IsPointer(type)) {
                    declaration = $"{type} {GetSnapshotName(expression)} = {expression};";
                } else {
                    int pointerIndex = expression.InvariantLastIndexOf(POINTER_ACCESS_MARKER);
                    expression = expression.Substring(0, pointerIndex);
                    type = _typeService.GetType(expression, operation);
                    declaration = $"{type} {GetSnapshotName(expression)} = {expression};";
                }

                return true;
            }

            declaration = null;
            return false;
        }

        private bool GetReplacementDeclaration(string expression, string operation, out string declaration, out int offset)
        {
            offset = 0;
            string variable = expression.Contains(POINTER_ACCESS_MARKER) ?
                expression.Split(POINTER_ACCESS_MARKER).First() :
                expression;

            if (_dataStructure.GlobalState.Contains(variable) || (_dataStructure[operation][variable] != null && _dataStructure[operation][variable].LinksToGlobalState))
            {
                string type = _typeService.GetType(expression, operation);

                if (_typeService.IsPointer(type))
                {
                    declaration = GetSnapshotName(expression);
                }
                else
                {
                    int pointerIndex = expression.InvariantLastIndexOf(POINTER_ACCESS_MARKER);
                    offset = expression.Length - pointerIndex;
                    expression = expression.Substring(0, pointerIndex);
                    declaration = GetSnapshotName(expression);
                }

                return true;
            }

            declaration = null;
            return false;
        }

        private static string GetSnapshotName(string expression)
        {
            string result = $"{SNAPSHOT_NAME_MARKER}{string.Join("", expression.Split(POINTER_ACCESS_MARKER).Select(x => x.Capitalize()))}";

            return result;
        }

        private List<RelationalExpression> GetRelations(CLanguageParser.SelectionStatementContext context) {
            List<RelationalExpression> relationalExpressions = context
                .expression()
                .GetLeafDescendants(x => x is CLanguageParser.EqualityExpressionContext)
                .Select(x => (object)x.Parent)
                .Select(GetRelationalExpression)
                .ToList();

            return relationalExpressions;
        }

        private RelationalExpression GetRelationalExpression(object context)
        {
            var type = context.GetType();

            if (!_relationExtractors.ContainsKey(type))
            {
                throw new NotSupportedException($"Type {type} is not supported");
            }

            return _relationExtractors[type](context);
        }

        private RelationalExpression GetRelationalExpression(CLanguageParser.EqualityExpressionContext context)
        {
            var leftExpression = context.equalityExpression().relationalExpression();
            var rightExpression = context.relationalExpression();

            var result = new RelationalExpression {
                LeftOperand = leftExpression.GetText(),
                RightOperand = rightExpression.GetText(),
                LeftOperandInterval = new KeyValuePair<int, int>(leftExpression.Start.StartIndex, leftExpression.Stop.StopIndex),
                RightOperandInterval = new KeyValuePair<int, int>(rightExpression.Start.StartIndex, rightExpression.Stop.StopIndex),
                Operation = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
        };

            return result;
        }

        private RelationalExpression GetRelationalExpression(CLanguageParser.AssignmentExpressionContext context) {
            var leftExpression = context.unaryExpression();
            var rightExpression = context.assignmentExpression();

            var result = new RelationalExpression {
                LeftOperand = leftExpression.GetText(),
                RightOperand = rightExpression.GetText(),
                LeftOperandInterval = new KeyValuePair<int, int>(leftExpression.Start.StartIndex, leftExpression.Stop.StopIndex),
                RightOperandInterval = new KeyValuePair<int, int>(rightExpression.Start.StartIndex, rightExpression.Stop.StopIndex),
                Operation = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
        };

            return result;
        }

        private RelationalExpression GetRelationalExpression(CLanguageParser.AndExpressionContext context)
        {
            var leftExpression = context.equalityExpression().relationalExpression().relationalExpression();
            var rightExpression = context.equalityExpression().relationalExpression().shiftExpression();

            var result = new RelationalExpression {
                LeftOperand = leftExpression.GetText(),
                RightOperand = rightExpression.GetText(),
                LeftOperandInterval = new KeyValuePair<int, int>(leftExpression.Start.StartIndex, leftExpression.Stop.StopIndex),
                RightOperandInterval = new KeyValuePair<int, int>(rightExpression.Start.StartIndex, rightExpression.Stop.StopIndex),
                Operation = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
        };

            return result;
        }

        private List<RelationalExpression> ExtractAssignments(CLanguageParser.SelectionStatementContext context)
        {
            /* TODO:
             * Currently, if we have
             * if(condition) {
             *    assign1;
             *    assign2;
             *    if(condition2) {...}
             *    assign3;
             * }
             * every assign expression will be extracted => we need to treat assign1/2 + assign3 separately
             */

            string functionName = context
                    .GetFunction()
                    .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>()
                    .GetName();
            IfStatement ifStatement = _dataStructure[functionName]
                .IfStatements
                .First(x => x.StartIndex == context.Start.StartIndex);
            List<RelationalExpression> expressions = ifStatement
                .Assignments
                .Select(GetRelationalExpression)
                .ToList();

            return expressions;
        }

        private class RelationalExpression {
            public string LeftOperand { get; set; }
            public string RightOperand { get; set; }
            public KeyValuePair<int, int> LeftOperandInterval { get; set; }
            public KeyValuePair<int, int> RightOperandInterval { get; set; }
            public string Operation { get; set; }
        }
    }
}
