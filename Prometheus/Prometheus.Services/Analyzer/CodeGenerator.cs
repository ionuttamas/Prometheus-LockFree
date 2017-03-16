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
        private const string UNMARKED_POINTER = "STATE_OP_NONE";

        private readonly DataStructure _dataStructure;
        private readonly RelationService _relationService;
        private readonly CodeUpdateTable _updateTable;

        public string CodeOutput { get; private set; }

        public CodeGenerator(DataStructure dataStructure, RelationService relationService) {
            _dataStructure = dataStructure;
            _relationService = relationService;
            _updateTable = new CodeUpdateTable();
        }

        public override object VisitCompilationUnit(CLanguageParser.CompilationUnitContext context)
        {
            AddFlags();
            AugmentStructures();
            AddFlagMethods();
            AddHelperMethods();

            return base.VisitCompilationUnit(context);
        }

        public override object VisitFunctionDefinition(CLanguageParser.FunctionDefinitionContext context)
        {
            AddWhileLoop(context);

            return base.VisitFunctionDefinition(context);
        }

        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context)
        {
            var relations = new List<RelationalExpression>();
            List<RelationalExpression> conditionRelations = _relationService.GetConditionRelations(context);
            List<RelationalExpression> assignmentRelations = _relationService.GetAssignmentRelations(context);
            relations.AddRange(conditionRelations);
            relations.AddRange(assignmentRelations);

            string variablesSnapshot = GetVariablesSnapshot(relations);
            string snapshotFlagCheck = GetSnapshotsFlagCheckExpression(relations);
            InsertionDeclaration snapshotAndCheckInsertion = new InsertionDeclaration(context.GetStartIndex(), $"{variablesSnapshot}{Environment.NewLine}{snapshotFlagCheck}");
            List<IDeclaration> conditionReplacements = GetConditionReplacements(conditionRelations);
            List<IDeclaration> assignmentReplacements = GetAssignmentsReplacements(assignmentRelations);

            _updateTable.Add(snapshotAndCheckInsertion);
            conditionReplacements.ForEach(_updateTable.Add);
            assignmentReplacements.ForEach(_updateTable.Add);

            return base.VisitSelectionStatement(context);
        }

        protected override void PreVisit(IParseTree tree, string input) {
        }

        protected override void PostVisit(IParseTree tree, string input) {
            CodeOutput = _updateTable.ApplyUpdates(input);
        }

        /// <summary>
        /// Adds the "while loop for this method.
        /// </summary>
        private void AddWhileLoop(CLanguageParser.FunctionDefinitionContext context) {
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
                .Select((var, ix) => new { Character = var, Index = ix })
                .First(x => char.IsWhiteSpace(x.Character))
                .Index;

            _updateTable.Add(new InsertionDeclaration(insertionIndex, new string(' ', offset) + "while (true) {"));
            _updateTable.Add(new InsertionDeclaration(context.GetStopIndex() - 1, "}"));
        }

        /// <summary>
        /// Add "expected" field to each pointer-based structure.
        /// </summary>
        private void AugmentStructures() {
            foreach (var structure in _dataStructure.Structures) {
                int structInsertIndex = structure.Context.structDeclarationList().GetStartIndex();
                string value = $"struct {structure.Name} * expected;";
                _updateTable.Add(new InsertionDeclaration(structInsertIndex, value));
            }
        }

        /// <summary>
        /// Adds the predefined flags that will be used in tag checking.
        /// </summary>
        private void AddFlags()
        {
            int index = _dataStructure.Structures.Min(x => x.StartIndex);
            _updateTable.Add(new InsertionDeclaration(index, $"#define {UNMARKED_POINTER} 0"));
        }

        //todo: this can be changed to generic, structure agnostic pointer tagging
        private void AddFlagMethods()
        {
            var builder = new StringBuilder();
            int index = _dataStructure.Structures.Max(x => x.EndIndex);

            var getFlag = "static inline uint64_t GETFLAG(void* ptr) {{" +
                                Environment.NewLine +
                                "      return ((uint64_t)ptr) & 8;" +
                                Environment.NewLine +
                                "}}";
            var setFlag = "static inline struct {0} * FLAG(void* ptr, uint64_t flag) {{" +
                                Environment.NewLine +
                                "      return (struct {0} *)(((uint64_t)ptr) & flag);" +
                                Environment.NewLine +
                                "}}";

            builder.AppendLine(getFlag);
            builder.AppendLine();

            foreach (var structure in _dataStructure.Structures)
            {
                builder.AppendLine(string.Format(setFlag, structure.Name));
            }

            _updateTable.Add(new InsertionDeclaration(index, builder.ToString()));
        }

        /// <summary>
        /// Add helper methods for each pointer-based structure.
        /// </summary>
        private void AddHelperMethods() {
            int helperMethodsIndex = _dataStructure.Structures.Max(x => x.EndIndex);
            string helperMethods = string.Join(Environment.NewLine, _dataStructure.Structures.Select(GetHelpMethod));
            _updateTable.Add(new InsertionDeclaration(helperMethodsIndex, helperMethods));
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

        private static string GetSnapshotsFlagCheckExpression(List<RelationalExpression> relations) {
            var snapshotVariables = relations
                .SelectMany(x => new List<VariableSnapshot> {x.LeftOperandSnapshot, x.RightOperandSnapshot})
                .Where(x => x != null)
                .Distinct()
                .Select(x => x.SnapshotVariable);
            var checkExpression = string.Join(Environment.NewLine,
                snapshotVariables.Select(x => $"if(GETFLAG({x})!={UNMARKED_POINTER}) {{ HELP HERE; continue; }}"));

            return checkExpression;
        }

        private List<IDeclaration> GetConditionReplacements(List<RelationalExpression> relations) {
            var result = new List<IDeclaration>();

            foreach (var relation in relations) {
                //todo: treat the null check case separately
                var leftOperandReplacement = GetReplacementDeclaration(relation.LeftOperand, relation.LeftOperandInterval, relation.Method);

                if(leftOperandReplacement!=null)
                    result.Add(leftOperandReplacement);

                var rightOperandReplacement = GetReplacementDeclaration(relation.RightOperand, relation.RightOperandInterval, relation.Method);

                if(rightOperandReplacement != null)
                    result.Add(rightOperandReplacement);
            }

            return result;
        }

        private List<IDeclaration> GetAssignmentsReplacements(List<RelationalExpression> relations)
        {
            if (!relations.Any())
                return new List<IDeclaration>();

            var result = new List<IDeclaration>();
            var builder = new StringBuilder();
            var method = relations[0].Method;

            foreach (var relation in relations)
            {
                var startIndex = relation.LeftOperandInterval.Start;
                var endIndex = relation.RightOperandInterval.End;

                if (relation.RightOperandSnapshot != null && relation.RightOperand != NULL_TOKEN)
                {
                    builder.AppendLine(GetCasCondition(relation.RightOperandSnapshot,
                        _dataStructure.GetRegionCode(method, startIndex)));
                }

                builder.AppendLine(GetCasCondition(relation.LeftOperandSnapshot,
                    _dataStructure.GetRegionCode(method, startIndex)));

                result.Add(new ReplacementDeclaration(startIndex, endIndex + 1, builder.ToString()));
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
                if (!expression.ContainsInvariant(POINTER_ACCESS_MARKER))
                {
                    declaration = $"{SNAPSHOT_NAME_MARKER}{string.Join("", expression.Split(POINTER_ACCESS_MARKER).Select(x => x.Capitalize()))}";
                }
                else
                {
                    int pointerIndex = expression.InvariantLastIndexOf(POINTER_ACCESS_MARKER);
                    offset = expression.Length - pointerIndex;
                    expression = expression.Substring(0, pointerIndex);
                    declaration = $"{SNAPSHOT_NAME_MARKER}{string.Join("", expression.Split(POINTER_ACCESS_MARKER).Select(x => x.Capitalize()))}";
                }

                return new ReplacementDeclaration(interval.Start, interval.End - offset, declaration);
            }

            return null;
        }

        private static string GetCasCondition(VariableSnapshot snapshot, int regionCode) {
            var checkAndMarkCondition = $"if(!CAS({snapshot.Variable}, {snapshot.SnapshotVariable}, FLAG({snapshot.SnapshotVariable}, {regionCode})) {{ HELP HERE; continue; }}";

            return checkAndMarkCondition;
        }

        private static string GetVariablesSnapshot(List<RelationalExpression> relations) {
            var builder = new StringBuilder();

            foreach (var relation in relations.Distinct(x => x.LeftOperand)) {
                if (relation.LeftOperandSnapshot != null) {
                    builder.AppendLine(relation.LeftOperandSnapshot + ";");
                }
            }

            foreach (var relation in relations.Distinct(x => x.RightOperand)) {
                if (relation.RightOperandSnapshot != null) {
                    builder.AppendLine(relation.RightOperandSnapshot + ";");
                }
            }

            return builder.ToString();
        }

        private class CodeUpdateTable {
            private readonly List<IDeclaration> _declarations;

            public CodeUpdateTable()
            {
                _declarations = new List<IDeclaration>();
            }

            public void Add(IDeclaration declaration)
            {
                _declarations.Add(declaration);
            }

            public string ApplyUpdates(string text)
            {
                foreach (var declaration in _declarations.OrderByDescending(x=>x.Index))
                {
                    text = declaration.ApplyOn(text);
                }

                return text;
            }
        }
    }
}