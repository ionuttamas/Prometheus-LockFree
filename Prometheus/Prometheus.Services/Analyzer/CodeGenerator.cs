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

            if (!string.IsNullOrEmpty(update.Value)) {
                _updateTable.Add(update.Key, update.Value);
            }

            return base.VisitSelectionStatement(context);
        }

        protected override void PreVisit(IParseTree tree, string input) {
        }

        protected override void PostVisit(IParseTree tree, string input) {
            CodeOutput = input;

            if (!_updateTable.Any())
                return;

            int offset = 0;

            foreach (var update in _updateTable) {
                var index = update.Key + offset;
                var indentOffset = index - CodeOutput.Substring(0, index).InvariantLastIndexOf(Environment.NewLine) - 2;
                var declarations = IndentDeclarations(update.Value, indentOffset);
                CodeOutput = CodeOutput.InsertAtIndex(declarations, index);
                offset += declarations.Length;
            }
        }

        private string IndentDeclarations(string declarations, int indentOffset) {
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

        private class CodeUpdateTable : IEnumerable<KeyValuePair<int, string>> {
            private const string EQUAL_MARKER = "=";
            private const string SEPARATOR_MARKER = " ";
            private Dictionary<int, string> _updates;

            public CodeUpdateTable() {
                _updates = new Dictionary<int, string>();
            }

            public void Add(int index, string input) {
                _updates[index] = input;
            }

            public IEnumerator<KeyValuePair<int, string>> GetEnumerator() {
                UpdateDeclarations();
                return _updates.OrderBy(x => x.Key).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            private void UpdateDeclarations() {
                var declaredVariables = new List<string>();
                var result = new Dictionary<int, string>();

                foreach (var update in _updates) {
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

                _updates = result;
            }

            private KeyValuePair<string, string> GetVariableAssignment(string declaration) {
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