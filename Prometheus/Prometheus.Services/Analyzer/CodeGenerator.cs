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

namespace Prometheus.Services
{
    public class CodeGenerator : CodeVisitor
    {
        private readonly CodeGenerationService _generationService;
        private readonly CodeUpdateTable _updateTable;

        public string CodeOutput { get; private set; }

        public CodeGenerator(CodeGenerationService generationService)
        {
            _generationService = generationService;
            _updateTable = new CodeUpdateTable();
        }

        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context)
        {
            KeyValuePair<int, string> update = _generationService.GetSnapshotDeclarations(context);

            if (!string.IsNullOrEmpty(update.Value))
            {
                _updateTable.Add(update.Key, update.Value);
            }

            return base.VisitSelectionStatement(context);
        }

        protected override void PreVisit(IParseTree tree, string input)
        {
        }

        protected override void PostVisit(IParseTree tree, string input)
        {
            CodeOutput = input;

            if(!_updateTable.Any())
                return;
            //todo: fix indenting
            int offset = 0;

            foreach (var update in _updateTable)
            {
                string declarations = update.Value;
                CodeOutput = CodeOutput.InsertAtIndex(declarations, update.Key + offset);
                offset += declarations.Length;
            }
        }

        private class CodeUpdateTable : IEnumerable<KeyValuePair<int, string>>
        {
            private readonly Dictionary<int, string> _updates;

            public CodeUpdateTable()
            {
                _updates = new Dictionary<int, string>();
            }

            public void Add(int index, string code)
            {
                _updates[index] = code;
            }

            public IEnumerator<KeyValuePair<int, string>> GetEnumerator()
            {
                return _updates.OrderBy(x => x.Key).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}