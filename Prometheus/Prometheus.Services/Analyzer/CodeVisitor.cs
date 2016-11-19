using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Prometheus.Services.Extensions;
using Prometheus.Services.Model;
using Prometheus.Services.Parser;

namespace Prometheus.Services
{
    public class CodeVisitor : CLanguageBaseVisitor<object>
    {
        private AntlrInputStream _inputStream;
        private CLanguageLexer _lexer;
        private CommonTokenStream _tokenStream;
        private CLanguageParser _parser;

        public CodeVisitor(DataStructure dataStructure)
        {
            DataStructure = dataStructure;
        }

        public DataStructure DataStructure { get; }

        public void Visit(string input)
        {
            _inputStream = new AntlrInputStream(input);
            _lexer = new CLanguageLexer(_inputStream);
            _tokenStream = new CommonTokenStream(_lexer);
            _parser = new CLanguageParser(_tokenStream);
            var tree = _parser.compilationUnit();
            Console.WriteLine(tree.ToStringTree(_parser));
            Visit(tree);
            DataStructure.ProcessDependencies();
        }

        public override object VisitFunctionDefinition(CLanguageParser.FunctionDefinitionContext context)
        {
            string functionName = context.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x=>x is CLanguageParser.DirectDeclaratorContext).GetName();
            DataStructure.AddOperation(functionName);

            return base.VisitFunctionDefinition(context);
        }

        public override object VisitAssignmentExpression(CLanguageParser.AssignmentExpressionContext context)
        {
            if (context.ChildCount == 3)
            {
                var functionName = context
                    .GetFunction()
                    .GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext)
                    .GetName();
                var operand = context
                    .unaryExpression()
                    .GetDirectDescendant<CLanguageParser.PrimaryExpressionContext>(x => x is CLanguageParser.PrimaryExpressionContext)
                    .GetName();
                List<string> dependentTokens = context
                    .assignmentExpression()
                    .GetDescendants(x => x is CLanguageParser.PrimaryExpressionContext)
                    .Select(x => ((CLanguageParser.PrimaryExpressionContext) x).GetName())
                    .ToList();

                if (DataStructure[functionName] != null && DataStructure[functionName][operand] == null)
                {
                    return base.VisitAssignmentExpression(context);
                }

                DataStructure.AddOperation(functionName, operand, dependentTokens);
            }

            return base.VisitAssignmentExpression(context);
        }

        public override object VisitDirectDeclarator(CLanguageParser.DirectDeclaratorContext context)
        {
            if (context.GetAncestor(x => x is CLanguageParser.StructDeclarationListContext) != null ||
                context.Parent.Parent is CLanguageParser.FunctionDefinitionContext) {
                return base.VisitDirectDeclarator(context);
            }

            var function = context.GetFunction();

            if (function == null)
            {
                DataStructure.AddGlobalVariable(context.GetName());
            }
            else
            {
                if (!(context.Parent.Parent.Parent is CLanguageParser.FunctionDefinitionContext) &&
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

                    DataStructure.AddOperation(functionName, context.GetName(), dependentTokens);
                }
            }

            return base.VisitDirectDeclarator(context);
        }

        public override object VisitTypedefName(CLanguageParser.TypedefNameContext context)
        {
            if (context.GetAncestor(x => x is CLanguageParser.StructDeclarationListContext) != null)
            {
                return base.VisitTypedefName(context);
            }

            var function = context.GetFunction();

            if (function == null)
            {
                DataStructure.AddGlobalVariable(context.GetName());
            }
            else
            {
                var functionName = function.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>(x => x is CLanguageParser.DirectDeclaratorContext).GetName();
                DataStructure.AddOperation(functionName, context.GetName(), new List<string>());
            }

            return base.VisitTypedefName(context);
        }
    }
}
