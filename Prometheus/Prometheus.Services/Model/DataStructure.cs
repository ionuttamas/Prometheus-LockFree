using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Common;

namespace Prometheus.Services.Model
{
    public class DataStructure
    {
        public List<Structure> Structures { get; set; }
        public State GlobalState { get; set; }
        public List<Operation> Operations { get; set; }
        public Dictionary<string, int> OperationCodes { get; }

        public DataStructure()
        {
            GlobalState = new State();
            Operations = new List<Operation>();
            Structures = new List<Structure>();
            OperationCodes = new Dictionary<string, int>();
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

        private void ExtractRegions(Operation operation)
        {
            operation.IfStatements
        }

        private Dictionary<int, int> GetSelectionRegions(IfStatement statement)
        {
            var result = new Dictionary<int, int>();

            if (statement == null)
                return result;

            if (statement.IfStatements.IsNullOrEmpty() && statement.ElseStatements.IsNullOrEmpty())
            {
                result[statement.StartIndex] = statement.EndIndex;
                return result;
            }

            if (statement.IfStatements.Any())
            {
                var regions = statement
                    .IfStatements
                    .Select(GetSelectionRegions)
                    .Aggregate(new Dictionary<int, int>(), (accumulator, value) => accumulator.Merge(value))
                    .OrderBy(x=>x.Key)
                    .ToList();

                result[statement.StartIndex] = regions[0].Key-1;

                for (int i = 1; i < regions.Count; i++)
                {
                    result[regions[i-1].Key] = regions[i-1].Value;

                    if (regions[i - 1].Value + 1 < regions[i].Key) //todo check equality
                    {
                        result[regions[i - 1].Value] = regions[i].Key;
                    }
                }

                result[regions.Last().Key] = regions.Last().Value;
                if (regions.Last().Value + 1 < statement.EndIndex) //todo check equality
                    {
                    result[regions.Last().Value] = statement.EndIndex;
                }
            }
        }

        private Dictionary<int, int> GetSelectionRegions(ElseStatement statement)
        {
            //todo
            var result = new Dictionary<int, int>();
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