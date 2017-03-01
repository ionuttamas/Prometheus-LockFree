using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime.Tree;
using Prometheus.Common;
using Prometheus.Services.Extensions;
using Prometheus.Services.Model;
using Prometheus.Services.Parser;
using Prometheus.Services.Service;

namespace Prometheus.Services {
    public class CodeGenerator : CodeVisitor {
        private readonly CodeGenerationService _generationService;
        private readonly CodeUpdateTable _updateTable;

        public string CodeOutput { get; private set; }

        public CodeGenerator(DataStructure dataStructure, CodeGenerationService generationService) {
            _generationService = generationService;
            _updateTable = new CodeUpdateTable(dataStructure);
        }

        public override object VisitFunctionDefinition(CLanguageParser.FunctionDefinitionContext context)
        {
            Dictionary<int, string> whileDeclarations = _generationService.GetWhileLoopDeclarations(context);

            foreach (var whileDeclaration in whileDeclarations)
            {
                _updateTable.AddInsertion(whileDeclaration.Key, whileDeclaration.Value);
            }

            return base.VisitFunctionDefinition(context);
        }

        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context) {
            List<RelationalExpression> relationalExpressions = _generationService.GetConditionRelations(context);
            relationalExpressions.AddRange(_generationService.GetInnerRelations(context));
            var update = new KeyValuePair<int, string>(context.GetStartIndex(),_generationService.GetSnapshotDeclarations(relationalExpressions));
            List<Tuple<int, int, string>> replacements = _generationService.GetReplacementDeclarations(context);

            if (!string.IsNullOrEmpty(update.Value)) {
                _updateTable.AddInsertion(update.Key, update.Value);
            }

            foreach (var replacement in replacements) {
                _updateTable.AddReplacement(replacement.Item1, replacement.Item2, replacement.Item3);
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

        private void Insert(KeyValuePair<int, string> insertion)
        {
            var index = insertion.Key;
            var indentOffset = index - CodeOutput.Substring(0, index).InvariantLastIndexOf(Environment.NewLine) - 2;
            var declarations = IndentDeclarations(insertion.Value, indentOffset);
            CodeOutput = CodeOutput.InsertAtIndex(declarations, index);
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
            private const string EQUAL_MARKER = "=";
            private const string SEPARATOR_MARKER = " ";
            private readonly DataStructure _dataStructure;
            private Dictionary<int, string> _insertions;
            private readonly Dictionary<int, KeyValuePair<int, string>> _replacements;

            public CodeUpdateTable(DataStructure dataStructure) {
                _insertions = new Dictionary<int, string>();
                _replacements = new Dictionary<int, KeyValuePair<int, string>>();
                _dataStructure = dataStructure;
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
                return value.Contains(EQUAL_MARKER);
            }

            private static KeyValuePair<string, string> GetVariableAssignment(string declaration) {
                string[] tokens = declaration
                    .Substring(0, declaration.InvariantIndexOf(EQUAL_MARKER))
                    .Trim()
                    .Split(SEPARATOR_MARKER);
                var assignmentExpression = declaration.Substring(declaration.InvariantIndexOf(EQUAL_MARKER));

                return new KeyValuePair<string, string>(tokens.Last(), $"{tokens.Last()} {assignmentExpression}");
            }
        }
    }
}