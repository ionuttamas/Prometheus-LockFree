using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Common;
using Prometheus.Services.Extensions;

namespace Prometheus.Services.Model
{
    public class DataStructure
    {
        public List<Structure> Structures { get; set; }
        public State GlobalState { get; set; }
        public List<Operation> Operations { get; set; }
        public Dictionary<string, int> OperationCodes { get; }
        public Dictionary<string, Dictionary<int, Dictionary<int, int>>> OperationInternalCodes { get; }

        public DataStructure()
        {
            GlobalState = new State();
            Operations = new List<Operation>();
            Structures = new List<Structure>();
            OperationCodes = new Dictionary<string, int>();
            OperationInternalCodes = new Dictionary<string, Dictionary<int, Dictionary<int, int>>>();
        }

        public Operation this[string name]
        {
            get { return Operations.FirstOrDefault(x => x.Name == name); }
        }

        public void AddStructure(Structure structure)
        {
            if (Structures.Any(x => x.Name == structure.Name))
                return;

            Structures.Add(structure);
        }

        public void AddGlobalVariable(Variable variable)
        {
            GlobalState.Add(variable);
        }

        public void AddOperation(Operation operation)
        {
            if (this[operation.Name] != null)
                return;

            Operations.Add(operation);
        }

        public void PostProcess()
        {
            ProcessDependencies();
            BuildCodesTable();
        }

        private void ProcessDependencies()
        {
            foreach (var operation in Operations)
            {
                ProcessOperation(operation);
            }
        }

        private void BuildCodesTable() {
            for (int i = 0; i < Operations.Count; i++)
            {
                OperationCodes[Operations[i].Name] = i;
            }

            ExtractRegions();
        }

        private void ProcessOperation(Operation operation)
        {
            List<string> operationVariables = GlobalState
                .Variables
                .Select(x=>x.Name)
                .Except(operation.LocalVariables.Select(x => x.Name))
                .ToList();

            foreach (var localVariable in operation.LocalVariables)
            {
                localVariable.DependentVariables
                    .RemoveWhere(x => !operationVariables.Contains(x) && !GlobalState.Contains(x));
                localVariable.DependentVariables
                    .RemoveWhere(x => x == localVariable.Name);
            }

            foreach (var localVariable in operation.LocalVariables)
            {
                if (localVariable.DependentVariables.Any(x => GlobalState.Contains(x)))
                {
                    LinkToGlobalState(localVariable);
                }
            }
        }

        private void ExtractRegions()
        {
            int codeCounter = 0;

            foreach (var operation in Operations)
            {
                Dictionary<int, int> operationRegions = ExtractOperationRegions(operation);

                OperationInternalCodes[operation.Name] = operationRegions
                    .ToDictionary(x => x.Key,
                                  x => new Dictionary<int, int> {{x.Key, codeCounter++}});
            }
        }

        private Dictionary<int, int> ExtractOperationRegions(Operation operation)
        {
            var result = new Dictionary<int,int>();

            if (operation.IfStatements.IsNullOrEmpty()) {
                result[operation.StartIndex] = operation.EndIndex;
                return result;
            }

            var regions = operation
                    .IfStatements
                    .Select(GetSelectionRegions)
                    .Aggregate(new Dictionary<int, Tuple<int, int>>(), (accumulator, value) => accumulator.Merge(value))
                    .OrderBy(x => x.Key)
                    .ToList();
            result[operation.StartIndex] = regions[0].Value.Item2;

            if (regions.Count > 1) {
                result[regions[0].Value.Item1] = regions[1].Value.Item2;
            }

            for (int i = 1; i < regions.Count; i++) {
                result[regions[i - 1].Value.Item1] = regions[i].Value.Item2;
            }

            result[regions.Last().Value.Item1] = operation.EndIndex;
            result.Merge(regions.ToDictionary(x => x.Key, x => x.Value.Item1));

            return result;
        }

        /// <summary>
        /// Returns for [1 if() {2 ... 3}] the entries: {2, {3, 1}} where 1,2,3 are the indexes
        /// </summary>
        private Dictionary<int, Tuple<int, int>> GetSelectionRegions(IfStatement statement) {
            var result = new Dictionary<int, Tuple<int, int>>();

            if (statement == null)
                return result;

            if (statement.IfStatements.IsNullOrEmpty() && statement.ElseStatements.IsNullOrEmpty()) {
                result[statement.Context.statement()[0].GetStartIndex()] = Tuple.Create(statement.EndIndex,
                    statement.StartIndex);
                return result;
            }

            var regions = statement
                .IfStatements
                .Select(GetSelectionRegions)
                .Concat(statement
                    .ElseStatements
                    .Select(GetSelectionRegions))
                .Aggregate(new Dictionary<int, Tuple<int, int>>(), (accumulator, value) => accumulator.Merge(value))
                .OrderBy(x => x.Key)
                .ToList();

            result[statement.Context.statement()[0].compoundStatement().GetStartIndex()] =
                Tuple.Create(statement.EndIndex, -1);

            if (regions.Count > 1) {
                result[regions[0].Value.Item1] = Tuple.Create(regions[1].Value.Item2, -1);
            }

            for (int i = 1; i < regions.Count; i++) {
                result[regions[i - 1].Value.Item1] = Tuple.Create(regions[i].Value.Item2, -1);
            }

            result[regions.Last().Value.Item1] = Tuple.Create(statement.EndIndex, -1);

            return result;
        }

        private Dictionary<int, Tuple<int, int>> GetSelectionRegions(ElseStatement statement)
        {
            var result = new Dictionary<int, Tuple<int, int>>();

            if (statement == null)
                return result;

            if (statement.IfStatements.IsNullOrEmpty()) {
                result[statement.Context.GetStartIndex()] = Tuple.Create(statement.EndIndex, statement.StartIndex);
                return result;
            }

            if (statement.IfStatements.Any()) {
                var regions = statement
                    .IfStatements
                    .Select(GetSelectionRegions)
                    .Aggregate(new Dictionary<int, Tuple<int, int>>(), (accumulator, value) => accumulator.Merge(value))
                    .OrderBy(x => x.Key)
                    .ToList();

                //todo: statement.Context => can be either a simple statement or a complex if-based statement
                result[statement.Context.GetStartIndex()] = Tuple.Create(statement.EndIndex, -1);

                if (regions.Count > 1) {
                    result[regions[0].Value.Item1] = Tuple.Create(regions[1].Value.Item2, -1);
                }

                for (int i = 1; i < regions.Count; i++) {
                    result[regions[i - 1].Value.Item1] = Tuple.Create(regions[i].Value.Item2, -1);
                }

                result[regions.Last().Value.Item1] = Tuple.Create(statement.EndIndex, -1);
            }

            return result;
        }

        private void LinkToGlobalState(Variable variable)
        {
            if (variable.LinksToGlobalState)
                return;

            variable.LinksToGlobalState = true;

            foreach (var dependentVariableName in variable.DependentVariables)
            {
                Variable dependentVariable = this[variable.Operation][dependentVariableName];

                if(dependentVariable==null)
                    continue;

                LinkToGlobalState(dependentVariable);
            }
        }
    }
}