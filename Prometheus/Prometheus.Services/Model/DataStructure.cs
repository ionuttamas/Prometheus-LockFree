using System;
using System.Collections.Generic;
using System.Linq;

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

        public void AddOperation(string name, int startIndex, int endIndex)
        {
            if (this[name] != null) return;

            var operation = new Operation(name)
            {
                StartIndex = startIndex,
                EndIndex = endIndex
            };

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