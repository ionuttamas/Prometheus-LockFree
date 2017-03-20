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
        private const string POINTER_ACCESS_MARKER = "->";
        private const string SNAPSHOT_NAME_MARKER = "old";
        private readonly DataStructure _dataStructure;
        private readonly TypeService _typeService;
        private readonly Dictionary<Type, Func<object, RelationalExpression>> _relationExtractors;

        public RelationService(DataStructure dataStructure, TypeService typeService)
        {
            _dataStructure = dataStructure;
            _typeService = typeService;
            _relationExtractors = new Dictionary<Type, Func<object, RelationalExpression>>
            {
                { typeof(CLanguageParser.EqualityExpressionContext), x => GetRelationalExpression((CLanguageParser.EqualityExpressionContext) x)},
                { typeof(CLanguageParser.AndExpressionContext), x => GetRelationalExpression((CLanguageParser.AndExpressionContext) x)}
            };
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

        public RelationalExpression GetRelationalExpression(object context)
        {
            var type = context.GetType();

            if (!_relationExtractors.ContainsKey(type))
            {
                throw new NotSupportedException($"Type {type} is not supported");
            }

            return _relationExtractors[type](context);
        }

        #region Relation extractors
        private RelationalExpression GetRelationalExpression(CLanguageParser.EqualityExpressionContext context) {
            var leftExpression = context.equalityExpression().relationalExpression();
            var rightExpression = context.relationalExpression();
            var method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName();

            var result = new RelationalExpression {
                LeftOperand = leftExpression.GetText(),
                RightOperand = rightExpression.GetText(),
                LeftOperandSnapshot = GetSnapshotDeclaration(leftExpression.GetText(), method),
                RightOperandSnapshot = GetSnapshotDeclaration(rightExpression.GetText(), method),
                LeftOperandInterval = new Interval(leftExpression.GetStartIndex(), leftExpression.GetStopIndex()),
                RightOperandInterval = new Interval(rightExpression.GetStartIndex(), rightExpression.GetStopIndex()),
                Method = method
            };

            return result;
        }

        private RelationalExpression GetRelationalExpression(CLanguageParser.AssignmentExpressionContext context) {
            var leftExpression = context.unaryExpression();
            var rightExpression = context.assignmentExpression();
            var method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName();

            var result = new RelationalExpression {
                LeftOperand = leftExpression.GetText(),
                RightOperand = rightExpression.GetText(),
                LeftOperandSnapshot = GetSnapshotDeclaration(leftExpression.GetText(), method),
                RightOperandSnapshot = GetSnapshotDeclaration(rightExpression.GetText(), method),
                LeftOperandInterval = new Interval(leftExpression.GetStartIndex(), leftExpression.GetStopIndex()),
                RightOperandInterval = new Interval(rightExpression.GetStartIndex(), rightExpression.GetStopIndex()),
                Method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
            };

            return result;
        }

        private RelationalExpression GetRelationalExpression(CLanguageParser.AndExpressionContext context) {
            var leftExpression = context.equalityExpression().relationalExpression().relationalExpression();
            var rightExpression = context.equalityExpression().relationalExpression().shiftExpression();
            var method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName();

            var result = new RelationalExpression {
                LeftOperand = leftExpression.GetText(),
                RightOperand = rightExpression.GetText(),
                LeftOperandSnapshot = GetSnapshotDeclaration(leftExpression.GetText(), method),
                RightOperandSnapshot = GetSnapshotDeclaration(rightExpression.GetText(), method),
                LeftOperandInterval = new Interval(leftExpression.GetStartIndex(), leftExpression.GetStopIndex()),
                RightOperandInterval = new Interval(rightExpression.GetStartIndex(), rightExpression.GetStopIndex()),
                Method = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName()
            };

            return result;
        }

        private VariableSnapshot GetSnapshotDeclaration(string expression, string operation) {
            string variable = expression.Contains(POINTER_ACCESS_MARKER) ?
                expression.Split(POINTER_ACCESS_MARKER).First() :
                expression;

            if (_dataStructure.HasGlobalVariable(variable) || (_dataStructure[operation][variable] != null && _dataStructure[operation][variable].LinksToGlobalState)) {
                string type = _typeService.GetType(expression, operation);

                if (!_typeService.IsPointer(type)) {
                    int pointerIndex = expression.InvariantLastIndexOf(POINTER_ACCESS_MARKER);
                    expression = expression.Substring(0, pointerIndex);
                    type = _typeService.GetType(expression, operation);
                }
                var result = new VariableSnapshot(type, GetSnapshotName(expression), expression);
                return result;
            }

            return null;
        }

        private static string GetSnapshotName(string expression) {
            string result = $"{SNAPSHOT_NAME_MARKER}{string.Join("", expression.Split(POINTER_ACCESS_MARKER).Select(x => x.Capitalize()))}";

            return result;
        }
        #endregion
    }
}
