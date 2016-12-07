using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Prometheus.Services.Extensions;
using Prometheus.Services.Model;
using Prometheus.Services.Parser;
using Prometheus.Common;

namespace Prometheus.Services
{
    public class DataStructureExtractor : CodeVisitor
    {
        private const string EQUALS_TOKEN = "=";
        private const string POINTER_TOKEN = "*";
        private const string SEMICOLUMN_TOKEN = ";";
        private const string STRUCT_TOKEN = "struct";
        private const string SEPARATOR_TOKEN = " ";

        public DataStructureExtractor(DataStructure dataStructure) {
            DataStructure = dataStructure;
        }

        public DataStructure DataStructure { get; }

        public override object VisitStructDeclarationList(CLanguageParser.StructDeclarationListContext context)
        {
            DataStructure.AddStructure(GetStructureType(context));

            return base.VisitStructDeclarationList(context);
        }

        public override object VisitFunctionDefinition(CLanguageParser.FunctionDefinitionContext context) {
            string functionName = context.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext).GetName();
            DataStructure.AddOperation(functionName);

            return base.VisitFunctionDefinition(context);
        }

        public override object VisitAssignmentExpression(CLanguageParser.AssignmentExpressionContext context) {
            if (context.ChildCount == 3) {
                var functionName = context
                    .GetFunction()
                    .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext)
                    .GetName();
                var operand = context
                    .unaryExpression()
                    .GetDirectDescendant<CLanguageParser.PrimaryExpressionContext>(x => x is CLanguageParser.PrimaryExpressionContext);
                List<string> dependentTokens = context
                    .assignmentExpression()
                    .GetDescendants(x => x is CLanguageParser.PrimaryExpressionContext)
                    .Select(x => ((CLanguageParser.PrimaryExpressionContext)x).GetName())
                    .ToList();
                string variableName = operand.GetName();

                if (DataStructure[functionName] != null && DataStructure[functionName][variableName] == null) {
                    return base.VisitAssignmentExpression(context);
                }

                DataStructure.AddOperation(functionName, variableName, string.Empty, dependentTokens);
            }

            return base.VisitAssignmentExpression(context);
        }

        public override object VisitDirectDeclarator(CLanguageParser.DirectDeclaratorContext context) {
            if (context.GetAncestor(x => x is CLanguageParser.StructDeclarationListContext) != null ||
                context.Parent.Parent is CLanguageParser.FunctionDefinitionContext)
            {
                return base.VisitDirectDeclarator(context);
            }

            var function = context.GetFunction();
            string variableName = context.GetName();

            if (function == null)
            {
                string type = GetType(context, variableName);
                Variable variable = new Variable(variableName, type, string.Empty);

                DataStructure.AddGlobalVariable(variable);
            }
            else if (!(context.Parent.Parent.Parent is CLanguageParser.FunctionDefinitionContext) &&
                     context.GetAncestor(x => x is CLanguageParser.ParameterListContext) == null)
            {
                var functionName = function.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext).GetName();
                List<string> dependentTokens;

                if (context.Parent.Parent is CLanguageParser.InitDeclaratorContext)
                {
                    dependentTokens = context
                        .Parent
                        .Parent
                        .GetDescendants(x => x is CLanguageParser.PrimaryExpressionContext)
                        .Select(x => ((CLanguageParser.PrimaryExpressionContext) x).GetName())
                        .ToList();
                }
                else
                {
                    dependentTokens = new List<string>();
                }

                string type = GetType(context, variableName);

                DataStructure.AddOperation(functionName, variableName, type, dependentTokens);
            }

            return base.VisitDirectDeclarator(context);
        }

        public override object VisitTypedefName(CLanguageParser.TypedefNameContext context) {
            if (context.GetAncestor(x => x is CLanguageParser.StructDeclarationListContext) != null) {
                return base.VisitTypedefName(context);
            }

            var function = context.GetFunction();
            string variableName = context.GetName();
            string type = GetType(context, variableName);

            if (function == null)
            {
                Variable variable = new Variable(variableName, type, string.Empty);

                DataStructure.AddGlobalVariable(variable);
            }
            else
            {
                var functionName =
                    function.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext).GetName();

                DataStructure.AddOperation(functionName, context.GetName(), type, new List<string>());
            }

            return base.VisitTypedefName(context);
        }

        public override void PreVisit(IParseTree tree, string input)
        {
        }

        public override void PostVisit(IParseTree tree, string input)
        {
            DataStructure.ProcessDependencies();
        }

        private Structure GetStructureType(CLanguageParser.StructDeclarationListContext context)
        {
            var structure = new Structure();
            var declarations = context.GetLeafDescendants(x => x is CLanguageParser.StructDeclarationContext).Select(x=>(CLanguageParser.StructDeclarationContext)x);
            var specifierContext = context.GetAncestor(x => x is CLanguageParser.StructOrUnionSpecifierContext);
            structure.Name = specifierContext.GetChild(1).GetText();

            foreach (var declaration in declarations)
            {
                string text = declaration.Start.InputStream.GetText(Interval.Of(declaration.Start.StartIndex, declaration.Stop.StopIndex));
                int index = text.InvariantLastIndexOf(SEPARATOR_TOKEN);
                string type = text.Substring(0, index).Trim(SEPARATOR_TOKEN);
                string name = text.Substring(index + 1).TrimEnd(SEMICOLUMN_TOKEN).Trim(SEPARATOR_TOKEN);
                int pointerIndex = name.InvariantLastIndexOf(POINTER_TOKEN);

                if (pointerIndex >= 0)
                {
                    type += name.Substring(0, pointerIndex+1);
                    name = name.TrimStart(POINTER_TOKEN);
                }

                var field = new Field
                {
                    Type = type,
                    Name = name
                };

                structure.Fields.Add(field);
            }

            return structure;
        }

        private static string GetType(ParserRuleContext context, string variableName)
        {
            var declarationContext = context.GetAncestor(x => x is CLanguageParser.DeclarationContext);

            if (declarationContext == null)
            {
                throw  new InvalidOperationException($"Could not get the type for context \"{context.GetName()}\"");
            }

            string result = string.Empty;
            string text = declarationContext.GetText();

            if (text.ContainsInvariant(SEMICOLUMN_TOKEN)) {
                text = text.TrimEnd(SEMICOLUMN_TOKEN);
            }

            if (text.ContainsInvariant(EQUALS_TOKEN))
            {
                text = text.TrimEnd(EQUALS_TOKEN);
            }

            text = text.Substring(0, text.InvariantIndexOf(variableName));

            if (text.Contains(STRUCT_TOKEN))
            {
                result = string.Format("{0} ",STRUCT_TOKEN);
                text = text.TrimStart(STRUCT_TOKEN);
            }

            result += text;

            return result;
        }
    }
}