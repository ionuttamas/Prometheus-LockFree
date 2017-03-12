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

    public class RelationService
    {
        private const string NULL_TOKEN = "NULL";
        private const string POINTER_ACCESS_MARKER = "->";
        private const string SNAPSHOT_NAME_MARKER = "old";
        private readonly DataStructure _dataStructure;
        private readonly TypeService _typeService;
        private readonly AtomicService _atomicService;
        private readonly Dictionary<Type, Func<object, RelationalExpression>> _relationExtractors;

        public RelationService(DataStructure dataStructure, TypeService typeService, AtomicService atomicService)
        {
            _dataStructure = dataStructure;
            _typeService = typeService;
            _atomicService = atomicService;
            _relationExtractors = new Dictionary<Type, Func<object, RelationalExpression>>
            {
                { typeof(CLanguageParser.EqualityExpressionContext), x => GetRelationalExpression((CLanguageParser.EqualityExpressionContext) x)},
                { typeof(CLanguageParser.AndExpressionContext), x => GetRelationalExpression((CLanguageParser.AndExpressionContext) x)}
            };
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
                    oldVariables.Add(snapshot.SnapshotVariable);
                }

                snapshot = GetSnapshotDeclaration(relationalExpression.RightOperand, relationalExpression.Method);

                if (snapshot != null)
                {
                    oldVariables.Add(snapshot.SnapshotVariable);
                }
            }

            return _atomicService.GetCheckFlagCondition(oldVariables.Distinct(x=>x).ToList());
        }

        public ReplacementDeclaration GetReplacementForAssignmentRelation(RelationalExpression relationalExpression)
        {
            var builder = new StringBuilder();
            var method = relationalExpression.Method;
            var snapshot = GetSnapshotDeclaration(relationalExpression.RightOperand, method);
            var startIndex = relationalExpression.LeftOperandInterval.Start;
            var endIndex = relationalExpression.RightOperandInterval.End;

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
                    result.Add(new ReplacementDeclaration(relationalExpression.LeftOperandInterval.Start, relationalExpression.LeftOperandInterval.End - offset, declaration));
                }

                if (GetReplacementDeclaration(relationalExpression.RightOperand, relationalExpression.Method, out declaration, out offset)) {
                    result.Add(new ReplacementDeclaration(relationalExpression.RightOperandInterval.Start, relationalExpression.RightOperandInterval.End - offset, declaration));
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

        public List<RelationalExpression> GetAssignmentRelations(CLanguageParser.SelectionStatementContext context) {
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

        private VariableSnapshot GetSnapshotDeclaration(string expression, string operation)
        {
            var result = new VariableSnapshot();
            string variable = expression.Contains(POINTER_ACCESS_MARKER) ?
                expression.Split(POINTER_ACCESS_MARKER).First() :
                expression;

            if (_dataStructure.HasGlobalVariable(variable) || (_dataStructure[operation][variable]!=null && _dataStructure[operation][variable].LinksToGlobalState))
            {
                string type = _typeService.GetType(expression, operation);

                if (!_typeService.IsPointer(type)) {
                    int pointerIndex = expression.InvariantLastIndexOf(POINTER_ACCESS_MARKER);
                    expression = expression.Substring(0, pointerIndex);
                    type = _typeService.GetType(expression, operation);
                }

                result.Type = type;
                result.SnapshotVariable = GetSnapshotName(expression);
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

            if (_dataStructure.HasGlobalVariable(variable) || (_dataStructure[operation][variable] != null && _dataStructure[operation][variable].LinksToGlobalState))
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

        public RelationalExpression GetRelationalExpression(object context)
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
                LeftOperandInterval = new Interval(leftExpression.GetStartIndex(), leftExpression.GetStopIndex()),
                RightOperandInterval = new Interval(rightExpression.GetStartIndex(), rightExpression.GetStopIndex()),
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
                LeftOperandInterval = new Interval(leftExpression.GetStartIndex(), leftExpression.GetStopIndex()),
                RightOperandInterval = new Interval(rightExpression.GetStartIndex(), rightExpression.GetStopIndex()),
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
                LeftOperandInterval = new Interval(leftExpression.GetStartIndex(), leftExpression.GetStopIndex()),
                RightOperandInterval = new Interval(rightExpression.GetStartIndex(), rightExpression.GetStopIndex()),
                Method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
        };

            return result;
        }
    }

    public class VariableSnapshot
    {
        public string Type { get; set; }
        public string SnapshotVariable { get; set; }
        public string Variable { get; set; }

        public override string ToString()
        {
            return $"{Type} {SnapshotVariable} = {Variable}";
        }
    }
}
