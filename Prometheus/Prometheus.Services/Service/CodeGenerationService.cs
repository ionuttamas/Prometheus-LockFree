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

    //todo: needs to be broken down into individual services and renamed
    public class CodeGenerationService
    {
        private const string NULL_TOKEN = "NULL";
        private const string POINTER_ACCESS_MARKER = "->";
        private const string SNAPSHOT_NAME_MARKER = "old";
        private readonly DataStructure _dataStructure;
        private readonly TypeService _typeService;
        private readonly OperationService _operationService;
        private readonly AtomicService _atomicService;
        private readonly Dictionary<Type, Func<object, RelationalExpression>> _relationExtractors;

        public CodeGenerationService(DataStructure dataStructure, TypeService typeService, OperationService operationService, AtomicService atomicService)
        {
            _dataStructure = dataStructure;
            _typeService = typeService;
            _operationService = operationService;
            _atomicService = atomicService;
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
            Method method = _dataStructure[operationName];
            IfStatement ifStatement = method.IfStatements.FirstOrDefault(x => x.StartIndex < insertionIndex && insertionIndex < x.EndIndex);

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

        /// <summary>
        /// Based on the assignment relations we generate an "if" statement for checking if any of the variable is marked.
        /// </summary>
        public string GetCheckForUnmarkedVariables(List<RelationalExpression> relationalExpressions)
        {
            var oldVariables = new List<string>();

            foreach (var relationalExpression in relationalExpressions)
            {
                var snapshot = GetSnapshotDeclaration(relationalExpression.LeftOperand, relationalExpression.Method);

                if (snapshot != null)
                {
                    oldVariables.Add(snapshot.Snapshot);
                }

                snapshot = GetSnapshotDeclaration(relationalExpression.RightOperand, relationalExpression.Method);

                if (snapshot != null)
                {
                    oldVariables.Add(snapshot.Snapshot);
                }
            }

            return _atomicService.GetCheckFlagCondition(oldVariables.Distinct(x=>x).ToList());
        }

        public ReplacementDeclaration GetReplacementForAssignmentRelation(RelationalExpression relationalExpression)
        {
            var builder = new StringBuilder();
            var method = relationalExpression.Method;
            var snapshot = GetSnapshotDeclaration(relationalExpression.RightOperand, method);
            var startIndex = relationalExpression.LeftOperandInterval.Key;
            var endIndex = relationalExpression.RightOperandInterval.Value;

            if (snapshot != null && relationalExpression.RightOperand != NULL_TOKEN)
            {
                builder.AppendLine(_atomicService.GetCheckAndMarkCondition(snapshot,
                    _dataStructure.GetRegionCode(method, startIndex)));

                snapshot = GetSnapshotDeclaration(relationalExpression.LeftOperand, method);
                builder.AppendLine(_atomicService.GetCheckAndMarkCondition(snapshot,
                    _dataStructure.GetRegionCode(method, startIndex)));
            }
            else
            {
                snapshot = GetSnapshotDeclaration(relationalExpression.LeftOperand, method);
                builder.AppendLine(_atomicService.GetCheckAndMarkCondition(snapshot,
                    _dataStructure.GetRegionCode(method, startIndex)));
            }

            return new ReplacementDeclaration(startIndex, endIndex + 1, builder.ToString());
        }

        public string GetSnapshotDeclarations(List<RelationalExpression> relationalExpressions)
        {
            var builder = new StringBuilder();

            foreach (var relationalExpression in relationalExpressions)
            {
                var snapshot = GetSnapshotDeclaration(relationalExpression.LeftOperand, relationalExpression.Method);
                if (snapshot != null)
                {
                    builder.AppendLine(snapshot.ToString());
                }

                snapshot = GetSnapshotDeclaration(relationalExpression.RightOperand, relationalExpression.Method);
                if (snapshot != null)
                {
                    builder.AppendLine(snapshot.ToString());
                }
            }

            return builder.ToString();
        }

        public string GetHelperOperations()
        {
            var result = string.Join(Environment.NewLine, _dataStructure.Structures.Select(x => _operationService.GetOperation(x).ToString()));

            return result;
        }

        public List<ReplacementDeclaration> GetStructuresAugmentation()
        {
            //_dataStructure.Structures.Select(x => x.Context.);

            return null;
        }

        public List<ReplacementDeclaration> GetReplacementDeclarations(CLanguageParser.SelectionStatementContext context)
        {
            var result = new List<ReplacementDeclaration>();
            List<RelationalExpression> relationalExpressions = context
                .expression()
                .GetLeafDescendants(x => x is CLanguageParser.EqualityExpressionContext)
                .Select(x => (object)x.Parent)
                .Select(GetRelationalExpression)
                .ToList();

            foreach (var relationalExpression in relationalExpressions) {
                string declaration;
                int offset;
                if (GetReplacementDeclaration(relationalExpression.LeftOperand, relationalExpression.Method, out declaration, out offset)) {
                    result.Add(new ReplacementDeclaration(relationalExpression.LeftOperandInterval.Key, relationalExpression.LeftOperandInterval.Value - offset, declaration));
                }

                if (GetReplacementDeclaration(relationalExpression.RightOperand, relationalExpression.Method, out declaration, out offset)) {
                    result.Add(new ReplacementDeclaration(relationalExpression.RightOperandInterval.Key, relationalExpression.RightOperandInterval.Value - offset, declaration));
                }
            }

            return result;
        }

        public List<RelationalExpression> GetConditionRelations(CLanguageParser.SelectionStatementContext context) {
            List<RelationalExpression> relationalExpressions = context
                .expression()
                .GetLeafDescendants(x => x is CLanguageParser.EqualityExpressionContext)
                .Select(x => (object)x.Parent)
                .Select(GetRelationalExpression)
                .ToList();

            return relationalExpressions;
        }

        public List<RelationalExpression> GetInnerRelations(CLanguageParser.SelectionStatementContext context) {
            List<RelationalExpression> relationalExpressions = ExtractAssignments(context);

            return relationalExpressions;
        }

        private VariableSnapshot GetSnapshotDeclaration(string expression, string operation)
        {
            var result = new VariableSnapshot();
            string variable = expression.Contains(POINTER_ACCESS_MARKER) ?
                expression.Split(POINTER_ACCESS_MARKER).First() :
                expression;

            if (_dataStructure.GlobalState.Contains(variable) || (_dataStructure[operation][variable]!=null && _dataStructure[operation][variable].LinksToGlobalState))
            {
                string type = _typeService.GetType(expression, operation);

                if (!_typeService.IsPointer(type)) {
                    int pointerIndex = expression.InvariantLastIndexOf(POINTER_ACCESS_MARKER);
                    expression = expression.Substring(0, pointerIndex);
                    type = _typeService.GetType(expression, operation);
                }

                result.Type = type;
                result.Snapshot = GetSnapshotName(expression);
                result.Variable = expression;
                return result;
            }

            return null;
        }

        private bool GetReplacementDeclaration(string expression, string operation, out string declaration, out int offset)
        {
            offset = 0;
            string variable = expression.Contains(POINTER_ACCESS_MARKER) ?
                expression.Split(POINTER_ACCESS_MARKER).First() :
                expression;

            if (_dataStructure.GlobalState.Contains(variable) || (_dataStructure[operation][variable] != null && _dataStructure[operation][variable].LinksToGlobalState))
            {
                declaration = GetSnapshotOldName(expression, operation, ref offset);
                return true;
            }

            declaration = null;
            return false;
        }

        private string GetSnapshotOldName(string expression, string operation, ref int offset)
        {
            string declaration;
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

            return declaration;
        }

        private static string GetSnapshotName(string expression)
        {
            string result = $"{SNAPSHOT_NAME_MARKER}{string.Join("", expression.Split(POINTER_ACCESS_MARKER).Select(x => x.Capitalize()))}";

            return result;
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
                Method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
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
                Method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
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
                Method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
        };

            return result;
        }

        private List<RelationalExpression> ExtractAssignments(CLanguageParser.SelectionStatementContext context) {
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
            IfStatement firstInnerIfStatement = ifStatement
                .IfStatements
                .MinItem(x => x.StartIndex);
            List<RelationalExpression> expressions = ifStatement
                .Assignments
                .Where(x => firstInnerIfStatement == null || firstInnerIfStatement.StartIndex > x.Start.StartIndex)
                .Select(GetRelationalExpression)
                .ToList();

            return expressions;
        }
    }

    public class VariableSnapshot
    {
        public string Type { get; set; }
        public string Snapshot { get; set; }
        public string Variable { get; set; }

        public override string ToString()
        {
            return $"{Type} {Snapshot} = {Variable}";
        }
    }

    public class ReplacementDeclaration
    {
        public int From { get; set; }
        public int To { get; set; }
        public string Value { get; set; }

        public ReplacementDeclaration(int from, int to, string value)
        {
            From = from;
            To = to;
            Value = value;
        }
    }
}
