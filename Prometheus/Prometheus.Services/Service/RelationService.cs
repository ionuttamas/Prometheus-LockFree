using System;
using System.Collections.Generic;
using System.Linq;
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
                { typeof(CLanguageParser.AndExpressionContext), x => GetRelationalExpression((CLanguageParser.AndExpressionContext) x)},
                { typeof(CLanguageParser.InitDeclaratorContext), x => GetRelationalExpression((CLanguageParser.InitDeclaratorContext) x)}
            };
        }

        public List<RelationalExpression> GetConditionRelations(CLanguageParser.FunctionDefinitionContext context) {
            var relations = context
                .GetDescendants<CLanguageParser.SelectionStatementContext>()
                .SelectMany(x => x.expression().GetLeafDescendants<CLanguageParser.EqualityExpressionContext>())
                .Select(x => (object)x.Parent)
                .Select(GetRelationalExpression)
                .ToList();

            return relations;
        }

        public List<RelationalExpression> GetConditionRelations(CLanguageParser.SelectionStatementContext context) {
            List<RelationalExpression> relationalExpressions = context
                .expression()
                .GetLeafDescendants<CLanguageParser.EqualityExpressionContext>()
                .Select(x => (object)x.Parent)
                .Select(GetRelationalExpression)
                .ToList();

            return relationalExpressions;
        }

        public List<RelationalExpression> GetAssignmentRelations(CLanguageParser.FunctionDefinitionContext context)
        {
            var relations = context
                .GetDescendants<CLanguageParser.AssignmentExpressionContext>()
                .Where(x => x.ChildCount > 1)
                .Select(GetRelationalExpression)
                .ToList();

            foreach (var relation in relations) {
                //todo: this is incorrect since we need to track only those relations from the root to this relation;
                //todo "if (condition) {1} else {2;3}" => for relation "3" we don't track relation "1"
                relation.PreviousRelations = relations
                    .Where(x => x.LeftOperandSnapshot != null || x.RightOperandSnapshot != null)
                    .Where(x => x.RightOperandInterval.End <= relation.LeftOperandInterval.Start)
                    .ToList();
            }

            return relations;
        }

        public List<RelationalExpression> GetInitializationRelations(CLanguageParser.FunctionDefinitionContext context)
        {
            var relations = context
                .GetDescendants<CLanguageParser.InitDeclaratorContext>()
                .Where(x => x.ChildCount > 1)
                .Select(GetRelationalExpression)
                .ToList();

            return relations;
        }

        public List<RelationalExpression> GetAssignmentRelations(CLanguageParser.SelectionStatementContext context) {
            // TODO: partial functionality
            string functionName = context
                .GetFunction()
                .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>()
                .GetName();
            IfStatement ifStatement = _dataStructure[functionName]
                .IfStatements
                .First(x => x.StartIndex == context.GetStartIndex());
            List<RelationalExpression> expressions = ifStatement
                .Assignments
                .Concat(ifStatement.ElseStatements.SelectMany(x=>x.Assignments))
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

        private RelationalExpression GetRelationalExpression(CLanguageParser.InitDeclaratorContext context) {
            var leftExpression = context.declarator().directDeclarator();
            var rightExpression = context.initializer();
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
                Method = method
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
                Method = method
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
                    if (pointerIndex < 0)
                        return null;

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
