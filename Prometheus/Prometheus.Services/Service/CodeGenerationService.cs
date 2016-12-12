using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public CodeGenerationService(DataStructure dataStructure, TypeService typeService)
        {
            _dataStructure = dataStructure;
            _typeService = typeService;
        }

        public KeyValuePair<int, string> GetSnapshotDeclarations(CLanguageParser.SelectionStatementContext context)
        {
            int index = context.Start.StartIndex;
            var builder = new StringBuilder();
            List<RelationalExpression> relationalExpressions = context
                .expression()
                .GetLeafDescendants(x => x is CLanguageParser.EqualityExpressionContext)
                .Select(x => (CLanguageParser.EqualityExpressionContext) x.Parent)
                .Select(GetRelationalExpression)
                .ToList();

            foreach (var relationalExpression in relationalExpressions)
            {
                string declaration;
                if (GetSnapshotDeclaration(relationalExpression.LeftOperand, relationalExpression.Operation, out declaration))
                {
                    builder.AppendLine(declaration);
                }

                if (GetSnapshotDeclaration(relationalExpression.RightOperand, relationalExpression.Operation, out declaration)) {
                    builder.AppendLine(declaration);
                }
            }

            return new KeyValuePair<int, string>(index, builder.ToString());
        }

        private bool GetSnapshotDeclaration(string expression, string operation, out string declaration )
        {
            string variable = expression.Contains(POINTER_ACCESS_MARKER) ?
                expression.Split(POINTER_ACCESS_MARKER).First() :
                expression;

            if (_dataStructure.GlobalState.Contains(variable) || _dataStructure[operation][variable].LinksToGlobalState)
            {
                string type = _typeService.GetType(expression, operation);
                declaration =  $"{type} {GetSnapshotName(expression)} = {expression};";

                return true;
            }

            declaration = null;
            return false;
        }

        private string GetSnapshotName(string expression)
        {
            var result = $"{SNAPSHOT_NAME_MARKER}{string.Join("", expression.Split(POINTER_ACCESS_MARKER).Select(x => x.Capitalize()))}";

            return result;
        }

        private RelationalExpression GetRelationalExpression(CLanguageParser.EqualityExpressionContext context) {
            var result = new RelationalExpression {
                LeftOperand = context.equalityExpression().relationalExpression().GetText(),
                RightOperand = context.relationalExpression().GetText(),
                Operation = context.GetFunction().GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext).GetName()
        };

            return result;
        }

        private class RelationalExpression {
            public string LeftOperand { get; set; }
            public string RightOperand { get; set; }
            public string Operation { get; set; }
        }
    }
}
