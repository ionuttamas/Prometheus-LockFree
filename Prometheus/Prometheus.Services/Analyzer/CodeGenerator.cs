using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime.Tree;
using Prometheus.Common;
using Prometheus.Services.Extensions;
using Prometheus.Services.Model;
using Prometheus.Services.Parser;
using Prometheus.Services.Service;

namespace Prometheus.Services {
    public class CodeGenerator : CodeVisitor {
        private const string NULL_TOKEN = "NULL";
        private const string POINTER_ACCESS_MARKER = "->";
        private const string SNAPSHOT_NAME_MARKER = "old";

        private readonly DataStructure _dataStructure;
        private readonly RelationService _relationService;
        private readonly CodeUpdateTable _updateTable;

        public string CodeOutput { get; private set; }

        public CodeGenerator(DataStructure dataStructure, RelationService relationService) {
            _dataStructure = dataStructure;
            _relationService = relationService;
            _updateTable = new CodeUpdateTable(dataStructure);
        }

        public override object VisitCompilationUnit(CLanguageParser.CompilationUnitContext context)
        {
            AugmentStructures();
            AddHelperMethods();

            return base.VisitCompilationUnit(context);
        }

        public override object VisitFunctionDefinition(CLanguageParser.FunctionDefinitionContext context)
        {
            AddWhileLoop(context);

            return base.VisitFunctionDefinition(context);
        }

        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context) {
            List<RelationalExpression> conditionRelations = _relationService.GetConditionRelations(context);
            List<RelationalExpression> innerRelations = _relationService.GetAssignmentRelations(context);
            conditionRelations.AddRange(innerRelations);
            string snapshotDeclarations = _relationService.GetSnapshotDeclarations(conditionRelations);
            string unmarkedVariablesCheckDeclaration = _relationService.GetCheckForUnmarkedVariables(conditionRelations);
            var update = new KeyValuePair<int, string>(context.GetStartIndex(), $"{snapshotDeclarations}{Environment.NewLine}{unmarkedVariablesCheckDeclaration}");
            List<IDeclaration> replacements = GetReplacementDeclarations(context);
            replacements.AddRange(innerRelations.Select(x=>_relationService.GetReplacementForAssignmentRelation(x)));

            if (!string.IsNullOrEmpty(update.Value)) {
                _updateTable.AddInsertion(update.Key, update.Value);
            }

            foreach (var replacement in replacements) {
                _updateTable.AddReplacement(replacement.From, replacement.To, replacement.Value);
            }

            return base.VisitSelectionStatement(context);
        }

        protected override void PreVisit(IParseTree tree, string input) {
        }

        protected override void PostVisit(IParseTree tree, string input) {
            CodeOutput = input;

            if (!_updateTable.IsEmpty())
                return;

            var updates = _updateTable
                .GetInsertions()
                //.Concat(nonAssignmentInsertions)
                .Where(x=>!string.IsNullOrEmpty(x.Value))
                .Select(x => new {Index = x.Key, Insert = x, Replace = default(KeyValuePair<int, KeyValuePair<int, string>>)})
                .Concat(_updateTable.GetReplacements().Select(x => new {Index = x.Key, Insert = default(KeyValuePair<int, string>), Replace = x}))
                .OrderByDescending(x => x.Index)
                .ToList();

            foreach (var update in updates) {
                if (!update.Insert.IsDefault())
                {
                    Insert(update.Insert);
                }
                else
                {
                    Replace(update.Replace);
                }
            }
        }

        /// <summary>
        /// Adds the "while loop for this method.
        /// </summary>
        private void AddWhileLoop(CLanguageParser.FunctionDefinitionContext context) {
            List<IDeclaration> declarations = GetWhileLoopDeclarations(context);

            foreach (var declaration in declarations)
                _updateTable.AddDeclaration(declaration);
        }

        /// <summary>
        /// Add "expected" field to each pointer-based structure.
        /// </summary>
        private void AugmentStructures() {
            foreach (var structure in _dataStructure.Structures) {
                int structInsertIndex = structure.Context.structDeclarationList().GetStartIndex();
                string value = $"struct {structure.Name} * expected;";
                _updateTable.AddDeclaration(new InsertionDeclaration(structInsertIndex, value));
            }
        }

        /// <summary>
        /// Add helper methods for each pointer-based structure.
        /// </summary>
        private void AddHelperMethods() {
            int helperMethodsIndex = _dataStructure.Structures.Max(x => x.EndIndex);
            string helperMethods = string.Join(Environment.NewLine, _dataStructure.Structures.Select(GetHelpMethod));
            _updateTable.AddDeclaration(new InsertionDeclaration(helperMethodsIndex, helperMethods));
        }

        /// <summary>
        /// Gets the helper method for the given structure type.
        /// </summary>
        private static string GetHelpMethod(Structure structure) {
            var argument = "value";
            //We just assign the expected argument to the current argument;
            //The method that manages to set the "operation" field on the argument via CAS instruction is the "owner" of the argument modification
            var functionDeclaration = $"void Help({structure.Name} * {argument}){{"
                                        + Environment.NewLine +
                                            $"{argument} = {argument}.expected;"
                                        + Environment.NewLine +
                                      $"}}";

            return functionDeclaration;
        }

        private List<IDeclaration> GetWhileLoopDeclarations(CLanguageParser.FunctionDefinitionContext context) {
            int insertionIndex = int.MaxValue;
            string operationName = context.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName();

            var localVariables = _dataStructure[operationName]
                .LocalVariables
                .OrderBy(x => x.Index)
                .ToList();
            var variable = localVariables
                .Skip(1) //todo:why??
                .Select((var, ix) => new { Variable = var, Index = ix })
                .FirstOrDefault(x => x.Variable.LinksToGlobalState);

            if (variable != null) {
                insertionIndex = localVariables[variable.Index].Index;
            }

            var bodyContext = context.compoundStatement();
            string body = bodyContext.GetContextText();
            var globalVariableIndexes = _dataStructure
                .GlobalVariables
                .Select(x => new Regex($"[^a-zA-Z\\d:]{x.Name}[^a-zA-Z\\d:]"))
                .Select(x => x.IsMatch(body) ? x.Match(body).Index : -1)
                .Where(x => x > 0)
                .Select(x => x + bodyContext.GetStartIndex());
            // If there is a global variable, we take the minimum between that index and the local variable that links to global state
            insertionIndex = Math.Min(insertionIndex, globalVariableIndexes.Any() ? globalVariableIndexes.Min() : insertionIndex);
            Method method = _dataStructure[operationName];

            // If the local or global variable is embedded in an "if" statement, we will embed the "if" statement in the "while" loop as well
            if (method.IfStatements.Any()) {
                var index = insertionIndex;
                var surroundingIfStatements = method.IfStatements.Where(x => x.Context.ContainsIndex(index));

                if (surroundingIfStatements.Any()) {
                    insertionIndex = Math.Min(insertionIndex, surroundingIfStatements.Min(x => x.StartIndex));
                }
            }

            insertionIndex = body.Substring(0, insertionIndex - bodyContext.GetStartIndex()).InvariantLastIndexOf(Environment.NewLine) + bodyContext.GetStartIndex() + 2;
            var offset = body.Substring(insertionIndex - bodyContext.GetStartIndex())
                .Select((var, ix) => new {Character = var, Index = ix})
                .First(x => char.IsWhiteSpace(x.Character))
                .Index;

            var result = new List<IDeclaration>
            {
                new InsertionDeclaration(insertionIndex, new string(' ', offset) + "while (true) {"),
                new InsertionDeclaration(context.GetStopIndex() - 1, "}")
            };

            return result;
        }

        private List<IDeclaration> GetReplacementDeclarations(CLanguageParser.SelectionStatementContext context) {
            var result = new List<IDeclaration>();
            List<RelationalExpression> relationalExpressions = context
                .expression()
                .GetLeafDescendants(x => x is CLanguageParser.EqualityExpressionContext)
                .Select(x => (object)x.Parent)
                .Select(_relationService.GetRelationalExpression)
                .ToList();

            foreach (var relation in relationalExpressions) {
                var leftOperandReplacement = GetReplacementDeclaration(relation.LeftOperand, relation.LeftOperandInterval, relation.Method);

                if(leftOperandReplacement!=null)
                    result.Add(leftOperandReplacement);

                var rightOperandReplacement = GetReplacementDeclaration(relation.RightOperand, relation.RightOperandInterval, relation.Method);

                if(rightOperandReplacement != null)
                    result.Add(rightOperandReplacement);
            }

            return result;
        }

        private IDeclaration GetReplacementDeclaration(string expression, Interval interval, string method) {
            int offset = 0;
            string declaration;
            string variable = expression.Contains(POINTER_ACCESS_MARKER) ?
                expression.Split(POINTER_ACCESS_MARKER).First() :
                expression;

            if (_dataStructure.HasGlobalVariable(variable) || (_dataStructure[method][variable] != null && _dataStructure[method][variable].LinksToGlobalState)) {
                if (expression.ContainsInvariant(POINTER_ACCESS_MARKER))
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

                return new ReplacementDeclaration(interval.Start, interval.End - offset, declaration);
            }

            return null;
        }

        private static string GetSnapshotName(string expression) {
            string result = $"{SNAPSHOT_NAME_MARKER}{string.Join("", expression.Split(POINTER_ACCESS_MARKER).Select(x => x.Capitalize()))}";

            return result;
        }

        private void Insert(KeyValuePair<int, string> insertion)
        {
            var index = insertion.Key;
            var indentOffset = index - CodeOutput.Substring(0, index).InvariantLastIndexOf(Environment.NewLine) - 2;
            var declarations = IndentDeclarations(insertion.Value, indentOffset);
            CodeOutput = CodeOutput.InsertAt(index, declarations);
        }

        private void Replace(KeyValuePair<int, KeyValuePair<int, string>> replacement)
        {
            CodeOutput = CodeOutput.Substring(0, replacement.Key) + replacement.Value.Value + CodeOutput.Substring(replacement.Value.Key+1);
        }

        private static string IndentDeclarations(string declarations, int indentOffset) {
            var declarationStatements = declarations
                .Split(Environment.NewLine)
                .ToList();
            string indentSpaces = string.Join("", Enumerable.Repeat(" ", indentOffset));
            var builder = new StringBuilder();
            builder.AppendLine(declarationStatements[0]);

            foreach (var declaration in declarations.Split(Environment.NewLine).Skip(1)) {
                builder.AppendLine(indentSpaces + declaration);
            }

            builder.Append(indentSpaces);

            return builder.ToString();
        }

        private class CodeUpdateTable {
            private const string ASSIGNMENT_MARKER = "=";
            private const string SEPARATOR_MARKER = " ";
            private readonly DataStructure _dataStructure;
            private Dictionary<int, string> _insertions;
            private readonly Dictionary<int, KeyValuePair<int, string>> _replacements;

            public CodeUpdateTable(DataStructure dataStructure) {
                _insertions = new Dictionary<int, string>();
                _replacements = new Dictionary<int, KeyValuePair<int, string>>();
                _dataStructure = dataStructure;
            }

            public void AddDeclaration<IDeclaration>(IDeclaration declaration)
            {

            }

            public void AddInsertion(int index, string value) {
                if (IsAssignment(value))
                {
                    if (_insertions.ContainsKey(index))
                    {
                        _insertions[index] = $"{_insertions[index]}{Environment.NewLine}{value}";
                    }
                    else
                    {
                        _insertions[index] = value;
                    }
                }
                else
                {
                    if (_insertions.ContainsKey(index)) {
                        _insertions[index] = $"{value}{Environment.NewLine}{_insertions[index]}";
                    } else {
                        _insertions[index] = value;
                    }
                }
            }

            public void AddReplacement(int startIndex, int endIndex, string value)
            {
                _replacements[startIndex] = new KeyValuePair<int, string>(endIndex, value);
            }

            public bool IsEmpty()
            {
                return _insertions.Any();
            }

            public IEnumerable<KeyValuePair<int, string>> GetInsertions() {
                UpdateDeclarations();
                return _insertions.OrderBy(x => x.Key);
            }

            public IEnumerable<KeyValuePair<int, KeyValuePair<int, string>>> GetReplacements() {
                return _replacements.OrderBy(x => x.Key);
            }

            private void UpdateDeclarations()
            {
                var result = new Dictionary<int, string>();
                var operationsDeclarations = _dataStructure.Operations.ToDictionary(x => x, x => new List<string>());

                foreach (var update in _insertions.OrderBy(x => x.Key))
                {
                    var builder = new StringBuilder();

                    foreach (var declaration in update.Value.Split(Environment.NewLine))
                    {
                        if (!IsAssignment(declaration))
                        {
                            builder.AppendLine(declaration);
                            continue;
                        }

                        KeyValuePair<string, string> assignment = GetVariableAssignment(declaration);
                        KeyValuePair<Method, List<string>> entry = operationsDeclarations
                            .First(x => x.Key.StartIndex < update.Key && update.Key < x.Key.EndIndex);

                        if (!entry.Value.Contains(assignment.Key))
                        {
                            builder.AppendLine(declaration);
                            entry.Value.Add(assignment.Key);
                        }
                    }

                    result[update.Key] = builder.ToString();
                }

                _insertions = result;
            }

            private static bool IsAssignment(string value)
            {
                return value.Contains(ASSIGNMENT_MARKER);
            }

            private static KeyValuePair<string, string> GetVariableAssignment(string declaration) {
                string[] tokens = declaration
                    .Substring(0, declaration.InvariantIndexOf(ASSIGNMENT_MARKER))
                    .Trim()
                    .Split(SEPARATOR_MARKER);
                var assignmentExpression = declaration.Substring(declaration.InvariantIndexOf(ASSIGNMENT_MARKER));

                return new KeyValuePair<string, string>(tokens.Last(), $"{tokens.Last()} {assignmentExpression}");
            }
        }
    }
}