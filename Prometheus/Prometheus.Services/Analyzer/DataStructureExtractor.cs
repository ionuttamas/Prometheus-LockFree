using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
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
            string functionName = context.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName();
            var bodyContext = context.compoundStatement();
            var operation = new Method(functionName)
            {
                StartIndex = bodyContext.Start.StartIndex,
                EndIndex = bodyContext.Stop.StopIndex,
                Context = bodyContext
            };
            DataStructure.AddOperation(operation);

            return base.VisitFunctionDefinition(context);
        }

        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context) {
            var assignments = context
                .GetFirstDescendant<CLanguageParser.CompoundStatementContext>()
                .GetFirstLevelDescendants<CLanguageParser.AssignmentExpressionContext>()
                .Where(x => x.ChildCount > 1)
                .ToList();
            var functionName = context
                .GetFunction()
                .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>()
                .GetName();
            var ifStatement = new IfStatement {
                Context = context,
                Assignments = assignments,
                ElseStatements = context
                                    .statement()
                                    .Skip(1)
                                    .Select(x => new ElseStatement {
                                        Context = x,
                                        Assignments = assignments
                                    })
                                    .ToList()
            };

            DataStructure[functionName].AddIfStatement(ifStatement);

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

                DataStructure[functionName].AddVariable(variableName, string.Empty, dependentTokens, context.Start.StartIndex);
            }

            return base.VisitAssignmentExpression(context);
        }

        public override object VisitDirectDeclarator(CLanguageParser.DirectDeclaratorContext context) {
            if (context.GetAncestor<CLanguageParser.StructDeclarationListContext>() != null ||
                context.Parent.Parent is CLanguageParser.FunctionDefinitionContext)
            {
                return base.VisitDirectDeclarator(context);
            }

            var function = context.GetFunction();
            string variableName = context.GetName();

            if (function == null)
            {
                string type = GetType(context);
                var variable = new Variable(variableName, type, string.Empty)
                {
                    Index = context.Start.StartIndex
                };

                DataStructure.AddGlobalVariable(variable);
            }
            else if (!(context.Parent.Parent.Parent is CLanguageParser.FunctionDefinitionContext) &&
                     context.GetAncestor<CLanguageParser.ParameterListContext>() == null)
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
                DataStructure[functionName].AddVariable(variableName, type, dependentTokens, context.Start.StartIndex);
            }

            return base.VisitDirectDeclarator(context);
        }

        public override object VisitTypedefName(CLanguageParser.TypedefNameContext context) {
            if (context.GetAncestor<CLanguageParser.StructDeclarationListContext>() != null) {
                return base.VisitTypedefName(context);
            }

            var function = context.GetFunction();
            string variableName = context.GetName();
            string type = GetType(context);

            if (function == null)
            {
                var variable = new Variable(variableName, type, string.Empty)
                {
                    Index = context.Start.StartIndex
                };
                DataStructure.AddGlobalVariable(variable);
            }
            else
            {
                var functionName = function
                    .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext)
                    .GetName();

                DataStructure[functionName].AddVariable(context.GetName(), type, new List<string>(), context.Start.StartIndex);
            }

            return base.VisitTypedefName(context);
        }

        protected override void PreVisit(IParseTree tree, string input)
        {
        }

        protected override void PostVisit(IParseTree tree, string input)
        {
            DataStructure.PostProcess();
        }

        private static Structure GetStructureType(CLanguageParser.StructDeclarationListContext context)
        {
            var declarations = context
                .GetLeafDescendants(x => x is CLanguageParser.StructDeclarationContext)
                .Select(x=>(CLanguageParser.StructDeclarationContext)x);
            var specifierContext = context.GetAncestor<CLanguageParser.StructOrUnionSpecifierContext>();
            var structure = new Structure(specifierContext.GetChild(1).GetText()) {
                Context = specifierContext
            };

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
            var declarationContext = context.GetAncestor<CLanguageParser.DeclarationContext>();

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