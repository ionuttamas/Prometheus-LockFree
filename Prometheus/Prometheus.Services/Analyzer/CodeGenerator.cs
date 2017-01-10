using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime.Tree;
using Prometheus.Common;
using Prometheus.Services.Parser;
using Prometheus.Services.Service;

namespace Prometheus.Services {
    public class CodeGenerator : CodeVisitor {
        private readonly CodeGenerationService _generationService;
        private readonly CodeUpdateTable _updateTable;

        public string CodeOutput { get; private set; }

        public CodeGenerator(CodeGenerationService generationService) {
            _generationService = generationService;
            _updateTable = new CodeUpdateTable();
        }

        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context) {
            KeyValuePair<int, string> update = _generationService.GetSnapshotDeclarations(context);
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

            int offset = 0;

            foreach (var update in _updateTable.GetInsertions()) {
                var index = update.Key + offset;
                var indentOffset = index - CodeOutput.Substring(0, index).InvariantLastIndexOf(Environment.NewLine) - 2;
                var declarations = IndentDeclarations(update.Value, indentOffset);
                CodeOutput = CodeOutput.InsertAtIndex(declarations, index);
                offset += declarations.Length;
            }
        }

        private static string IndentDeclarations(string declarations, int indentOffset) {
            string[] declarationStatements = declarations.Split(Environment.NewLine);
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
            private Dictionary<int, string> _insertions;
            private readonly Dictionary<int, KeyValuePair<int, string>> _replacements;

            public CodeUpdateTable() {
                _insertions = new Dictionary<int, string>();
                _replacements = new Dictionary<int, KeyValuePair<int, string>>();
            }

            public void AddInsertion(int index, string value) {
                _insertions[index] = value;
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

            private void UpdateDeclarations() {
                var declaredVariables = new List<string>();
                var result = new Dictionary<int, string>();

                foreach (var update in _insertions) {
                    var builder = new StringBuilder();

                    foreach (var declaration in update.Value.Split(Environment.NewLine)) {
                        var assignment = GetVariableAssignment(declaration);

                        if (declaredVariables.Contains(assignment.Key)) {
                            builder.AppendLine(assignment.Value);
                        } else {
                            builder.AppendLine(declaration);
                        }

                        declaredVariables.Add(assignment.Key);
                    }

                    result[update.Key] = builder.ToString();
                }

                _insertions = result;
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