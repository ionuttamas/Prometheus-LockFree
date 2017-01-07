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
        private const string SEPARATOR_TOKEN = " ";

        public DataStructureExtractor() {
            DataStructure = new DataStructure();
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

        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context)
        {
            var assignments = context
                .GetFirstDescendant<CLanguageParser.CompoundStatementContext>()
                .GetFirstLevelDescendants<CLanguageParser.AssignmentExpressionContext>()
                .Where(x=>x.ChildCount>1)
                .ToList();
            var functionName = context
                    .GetFunction()
                    .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>()
                    .GetName();
            var ifStatement = new IfStatement
            {
                Index = context.Start.StartIndex,
                Assignments = assignments
            };

            DataStructure[functionName].IfStatements.Add(ifStatement);

            return base.VisitSelectionStatement(context);
        }

        public override object VisitAssignmentExpression(CLanguageParser.AssignmentExpressionContext context) {
            if (context.ChildCount == 3) {
                var functionName = context
                    .GetFunction()
                    .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>()
                    .GetName();
                var operand = context
                    .unaryExpression()
                    .GetDirectDescendant<CLanguageParser.PrimaryExpressionContext>();
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
                string type = GetType(context);
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

                string type = GetType(context);

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
            string type = GetType(context);

            if (function == null)
            {
                Variable variable = new Variable(variableName, type, string.Empty);

                DataStructure.AddGlobalVariable(variable);
            }
            else
            {
                var functionName = function
                    .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext)
                    .GetName();

                DataStructure.AddOperation(functionName, context.GetName(), type, new List<string>());
            }

            return base.VisitTypedefName(context);
        }

        protected override void PreVisit(IParseTree tree, string input)
        {
        }

        protected override void PostVisit(IParseTree tree, string input)
        {
            DataStructure.ProcessDependencies();
        }

        private Structure GetStructureType(CLanguageParser.StructDeclarationListContext context)
        {
            var declarations = context.GetLeafDescendants(x => x is CLanguageParser.StructDeclarationContext).Select(x=>(CLanguageParser.StructDeclarationContext)x);
            var specifierContext = context.GetAncestor(x => x is CLanguageParser.StructOrUnionSpecifierContext);
            var structure = new Structure(specifierContext.GetChild(1).GetText());

            foreach (var declaration in declarations)
            {
                string text = declaration.GetContextText();
                int index = text.InvariantLastIndexOf(SEPARATOR_TOKEN);
                string type = text.Substring(0, index).Trim(SEPARATOR_TOKEN);
                string name = text.Substring(index + 1).TrimEnd(SEMICOLUMN_TOKEN).Trim(SEPARATOR_TOKEN);
                int pointerIndex = name.InvariantLastIndexOf(POINTER_TOKEN);

                if (pointerIndex >= 0)
                {
                    type += name.Substring(0, pointerIndex+1);
                    name = name.TrimStart(POINTER_TOKEN);
                }

                var field = new Field(type, name);

                structure.Fields.Add(field);
            }

            return structure;
        }

        private static string GetType(ParserRuleContext context)
        {
            var declarationContext = (ParserRuleContext)context.GetAncestor(x => x is CLanguageParser.DeclarationContext);

            if (declarationContext == null)
            {
                throw  new InvalidOperationException($"Could not get the type for context \"{context.GetName()}\"");
            }

            string text = declarationContext.GetContextText();

            if (text.ContainsInvariant(SEMICOLUMN_TOKEN)) {
                text = text.TrimEnd(SEMICOLUMN_TOKEN);
            }

            if (text.ContainsInvariant(EQUALS_TOKEN))
            {
                text = text.Substring(0, text.InvariantLastIndexOf(EQUALS_TOKEN));
            }

            text = text.TrimEnd(SEPARATOR_TOKEN);

            string result;

            if (text.Contains(POINTER_TOKEN))
            {
                result = string.Format("{0} {1}",
                    text.Substring(0, text.InvariantIndexOf(POINTER_TOKEN)).TrimEnd(SEPARATOR_TOKEN),
                    text.Substring(text.InvariantIndexOf(POINTER_TOKEN), text.InvariantLastIndexOf(POINTER_TOKEN)-text.InvariantIndexOf(POINTER_TOKEN) + 1));
            }
            else
            {
                result = text.Substring(0, text.InvariantLastIndexOf(SEPARATOR_TOKEN));
            }

            result = result.RemoveDuplicateSpaces();

            return result;
        }
    }
}