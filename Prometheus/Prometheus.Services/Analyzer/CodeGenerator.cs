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
    public class CodeGenerator : CodeVisitor
    {
        private const string NULL_TOKEN = "NULL";
        private const string POINTER_ACCESS_MARKER = "->";
        private const string SNAPSHOT_NAME_MARKER = "old";
        private const string UNMARKED_POINTER = "STATE_OP_NONE";

        private readonly DataStructure _dataStructure;
        private readonly RelationService _relationService;
        private readonly CodeUpdateTable _updateTable;

        public string CodeInput { get; private set; }
        public string CodeOutput { get; private set; }

        public CodeGenerator(DataStructure dataStructure, RelationService relationService)
        {
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
            var index = AddWhileLoop(context);
            ProcessAssignments(context, index);

            return base.VisitFunctionDefinition(context);
        }

        private void ProcessAssignments(CLanguageParser.FunctionDefinitionContext context, int index)
        {
            List<RelationalExpression> conditionRelations = _relationService.GetConditionRelations(context);
            List<RelationalExpression> assignmentRelations = _relationService.GetAssignmentRelations(context);
            List<RelationalExpression> initDeclarationRelations = _relationService.GetInitializationRelations(context);
            List<string> nullCheckOperands = conditionRelations
                .Select(x => x.RightOperand == NULL_TOKEN ? x.LeftOperand : (x.LeftOperand == NULL_TOKEN ? x.RightOperand : null))
                .Where(x => x != null)
                .ToList();
            //todo: this is not right: tail = head->next; if a check is made for head
            //todo: further reconsider this: in the case of a "check null and return", further assignments can be replaced with old* correspondents
            //assignmentRelations
            //    .RemoveAll(x => nullCheckOperands.Any(op => x.LeftOperand.ContainsInvariant($"{op}{POINTER_ACCESS_MARKER}") ||
            //                                                x.RightOperand.ContainsInvariant($"{op}{POINTER_ACCESS_MARKER}")));
            var relations = new List<RelationalExpression>();
            relations.AddRange(conditionRelations);
            relations.AddRange(assignmentRelations);

            string variablesSnapshot = GetVariablesSnapshot(relations);
            string snapshotFlagCheck = GetSnapshotsFlagCheckExpression(relations);

            //todo: fix indenting
            //var indent = index - CodeInput.Substring(0, index).InvariantLastIndexOf(Environment.NewLine);
            InsertionDeclaration snapshotAndCheckInsertion = new InsertionDeclaration(index+1, $"{variablesSnapshot}{Environment.NewLine}{snapshotFlagCheck}".Indent(51));
            List<IDeclaration> conditionReplacements = GetConditionReplacements(conditionRelations);
            List<IDeclaration> assignmentReplacements = GetCasAssignmentsReplacements(assignmentRelations);
            List<IDeclaration> initReplacements = GetInitDeclarationsReplacements(initDeclarationRelations);

            _updateTable.Add(snapshotAndCheckInsertion);
            conditionReplacements.ForEach(_updateTable.Add);
            assignmentReplacements.ForEach(_updateTable.Add);
            initReplacements.ForEach(_updateTable.Add);
        }

        /*//todo: we should extract all assignments not only those per selection statement
        public override object VisitSelectionStatement(CLanguageParser.SelectionStatementContext context)
        {
            List<RelationalExpression> conditionRelations = _relationService.GetConditionRelations(context);
            List<RelationalExpression> assignmentRelations = _relationService.GetAssignmentRelations(context);
            //todo: this check needs to be more robust - take all the null checks and see if they are actually used
            List<string> nullCheckOperands = conditionRelations
                .Select(x => x.RightOperand == NULL_TOKEN ? x.LeftOperand : (x.LeftOperand == NULL_TOKEN ? x.RightOperand : null))
                .Where(x => x != null)
                .ToList();
            //todo: this is not right: tail = head->next; if a check is made for head
            assignmentRelations
                .RemoveAll(x => nullCheckOperands.Any(op => x.LeftOperand.ContainsInvariant($"{op}{POINTER_ACCESS_MARKER}") ||
                                                            x.RightOperand.ContainsInvariant($"{op}{POINTER_ACCESS_MARKER}")));
            var relations = new List<RelationalExpression>();
            relations.AddRange(conditionRelations);
            relations.AddRange(assignmentRelations);

            string variablesSnapshot = GetVariablesSnapshot(relations);
            string snapshotFlagCheck = GetSnapshotsFlagCheckExpression(relations);
            InsertionDeclaration snapshotAndCheckInsertion = new InsertionDeclaration(context.GetStartIndex(),
                $"{variablesSnapshot}{Environment.NewLine}{snapshotFlagCheck}");
            List<IDeclaration> conditionReplacements = GetConditionReplacements(conditionRelations);
            List<IDeclaration> assignmentReplacements = GetAssignmentsReplacements(assignmentRelations);

            _updateTable.Add(snapshotAndCheckInsertion);
            conditionReplacements.ForEach(_updateTable.Add);
            assignmentReplacements.ForEach(_updateTable.Add);

            return base.VisitSelectionStatement(context);
        }
*/
        protected override void PreVisit(IParseTree tree, string input)
        {
            CodeInput = input;
        }

        protected override void PostVisit(IParseTree tree, string input)
        {
            CodeOutput = _updateTable.ApplyUpdates(input);
        }

        /// <summary>
        /// Adds the "while loop for this method and returns the "while" loop insertion index.
        /// </summary>
        private int AddWhileLoop(CLanguageParser.FunctionDefinitionContext context)
        {
            int insertionIndex = int.MaxValue;
            string operationName = context.GetFirstDescendant<CLanguageParser.DirectDeclaratorContext>().GetName();

            var localVariables = _dataStructure[operationName]
                .LocalVariables
                .OrderBy(x => x.Index)
                .ToList();
            var variable = localVariables
                .Skip(1) //todo:why??
                .Select((var, ix) => new {Variable = var, Index = ix})
                .FirstOrDefault(x => x.Variable.LinksToGlobalState);

            if (variable != null)
            {
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
            if (method.IfStatements.Any())
            {
                var index = insertionIndex;
                var surroundingIfStatements = method.IfStatements.Where(x => x.Context.ContainsIndex(index));

                if (surroundingIfStatements.Any())
                {
                    insertionIndex = Math.Min(insertionIndex, surroundingIfStatements.Min(x => x.StartIndex));
                }
            }

            string trimmedBody = body.Substring(0, insertionIndex - bodyContext.GetStartIndex());
            insertionIndex = trimmedBody.InvariantLastIndexOf(Environment.NewLine) + bodyContext.GetStartIndex() + 2;
            int offset = trimmedBody.Length - trimmedBody.InvariantLastIndexOf(Environment.NewLine) - 2;

            _updateTable.Add(new InsertionDeclaration(insertionIndex, new string(' ', offset) + "while (true) {"));
            _updateTable.Add(new InsertionDeclaration(context.GetStopIndex() - 1, "}"));

            return insertionIndex;
        }

        /// <summary>
        /// Add "expected" field to each pointer-based structure.
        /// </summary>
        private void AugmentStructures()
        {
            foreach (var structure in _dataStructure.Structures)
            {
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
        private void AddHelperMethods()
        {
            int helperMethodsIndex = _dataStructure.Structures.Max(x => x.EndIndex);
            string helperMethods = string.Join(Environment.NewLine, _dataStructure.Structures.Select(GetHelpMethod));
            _updateTable.Add(new InsertionDeclaration(helperMethodsIndex, helperMethods));
        }

        /// <summary>
        /// Gets the helper method for the given structure type.
        /// </summary>
        private static string GetHelpMethod(Structure structure)
        {
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

        private static string GetSnapshotsFlagCheckExpression(List<RelationalExpression> relations)
        {
            var snapshotVariables = relations
                .SelectMany(x => new List<VariableSnapshot> {x.LeftOperandSnapshot, x.RightOperandSnapshot})
                .Where(x => x != null)
                .Distinct()
                .Select(x => x.SnapshotVariable);
            var checkExpression = string.Join(Environment.NewLine,
                snapshotVariables.Select(x => $"if(GETFLAG({x})!={UNMARKED_POINTER}) {{ HELP HERE; continue; }}"));

            return checkExpression;
        }

        private List<IDeclaration> GetInitDeclarationsReplacements(List<RelationalExpression> relations)
        {
            var result = new List<IDeclaration>();

            foreach (var relation in relations)
            {
                var rightOperandReplacement = GetReplacementDeclaration(relation.RightOperand, relation.RightOperandInterval, relation.Method);

                if (rightOperandReplacement != null)
                    result.Add(rightOperandReplacement);
            }

            return result;
        }

        private List<IDeclaration> GetConditionReplacements(List<RelationalExpression> relations)
        {
            var result = new List<IDeclaration>();

            foreach (var relation in relations)
            {
                //todo: treat the null check case separately
                var leftOperandReplacement = GetReplacementDeclaration(relation.LeftOperand, relation.LeftOperandInterval, relation.Method);

                if (leftOperandReplacement != null)
                    result.Add(leftOperandReplacement);

                var rightOperandReplacement = GetReplacementDeclaration(relation.RightOperand, relation.RightOperandInterval, relation.Method);

                if (rightOperandReplacement != null)
                    result.Add(rightOperandReplacement);
            }

            return result;
        }

        private List<IDeclaration> GetCasAssignmentsReplacements(List<RelationalExpression> relations)
        {
            if (!relations.Any())
                return new List<IDeclaration>();

            var result = new List<IDeclaration>();
            var method = relations[0].Method;

            foreach (var relation in relations)
            {
                if(relation.LeftOperandSnapshot==null && relation.RightOperandSnapshot==null)
                    continue;

                var startIndex = relation.LeftOperandInterval.Start;
                var endIndex = relation.RightOperandInterval.End;
                var casConditions = GetCasCondition(relation, _dataStructure.GetRegionCode(method, startIndex));

                result.Add(new ReplacementDeclaration(startIndex, endIndex + 1, casConditions));
            }

            return result;
        }

        private IDeclaration GetReplacementDeclaration(string expression, Interval interval, string method)
        {
            int offset = 0;
            string declaration;
            string variable = expression.Contains(POINTER_ACCESS_MARKER)
                ? expression.Split(POINTER_ACCESS_MARKER).First()
                : expression;

            if (_dataStructure.HasGlobalVariable(variable) || (_dataStructure[method][variable] != null && _dataStructure[method][variable].LinksToGlobalState))
            {
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

        private static string GetCasCondition(RelationalExpression relation, int regionCode)
        {
            var casConditionFormat = "if(!CAS({0}, {1}, FLAG({2}, {3})) {{ continue; }}";
            var result = string.Empty;

            if (relation.RightOperand == NULL_TOKEN)
            {
                var snapshot = relation.LeftOperandSnapshot;
                result += string.Format(casConditionFormat, snapshot.Variable, snapshot.SnapshotVariable, relation.RightOperand, regionCode);
            }
            else if (relation.RightOperandSnapshot == null)
            {
                // The assigned variable is not linked to the global state
                var snapshot = relation.LeftOperandSnapshot;
                result += string.Format(casConditionFormat, snapshot.Variable, snapshot.SnapshotVariable, relation.RightOperand, regionCode);
            }
            else
            {
                // The assigned variable is linked to the global state
                var snapshot = relation.RightOperandSnapshot;
                result += string.Format(casConditionFormat, snapshot.Variable, snapshot.SnapshotVariable, snapshot.SnapshotVariable, regionCode);

                snapshot = relation.LeftOperandSnapshot;
                result += Environment.NewLine + string.Format(casConditionFormat, snapshot.Variable, snapshot.SnapshotVariable, relation.RightOperand, regionCode);
            }

            return result;
        }

        private static string GetVariablesSnapshot(List<RelationalExpression> relations)
        {
            var result = string.Empty;

            foreach (var relation in relations.Distinct(x => x.LeftOperand).Where(x => x.LeftOperandSnapshot != null))
            {
                result += $"{relation.LeftOperandSnapshot};{Environment.NewLine}";
            }

            foreach (var relation in relations.Distinct(x => x.RightOperand).Where(x => x.RightOperandSnapshot != null))
            {
                if (!result.Contains(relation.RightOperandSnapshot.ToString()))
                {
                    result += $"{relation.RightOperandSnapshot};{Environment.NewLine}";
                }
            }

            return result;
        }

        private class CodeUpdateTable
        {
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
                foreach (var declaration in _declarations.OrderByDescending(x => x.Index))
                {
                    text = declaration.ApplyOn(text);
                }

                return text;
            }
        }
    }
}