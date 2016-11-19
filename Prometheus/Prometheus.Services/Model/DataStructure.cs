using System.Collections.Generic;
using System.Linq;

namespace Prometheus.Services.Model
{
    public class DataStructure
    {
        public State GlobalState { get; set; }
        public List<Operation> Operations { get; set; }

        public DataStructure()
        {
            GlobalState = new State();
            Operations = new List<Operation>();
        }

        public Operation this[string name]
        {
            get { return Operations.FirstOrDefault(x => x.Name == name); }
        }

        public void AddGlobalVariable(string name)
        {
            GlobalState.Add(name);
        }

        public void AddOperation(string name)
        {
            if (this[name] != null) return;

            var operation = new Operation(name);
            Operations.Add(operation);
        }

        public void AddOperation(string name, string variableName, List<string> dependentVariables) {
            AddOperation(name);
            this[name].AddVariable(variableName, dependentVariables);
        }

        public void ProcessDependencies()
        {
            foreach (var operation in Operations)
            {
                ProcessOperation(operation);
            }
        }

        private void ProcessOperation(Operation operation)
        {
            List<string> operationVariables = GlobalState
                .Variables
                .Except(operation.LocalVariables.Select(x => x.Name))
                .ToList();

            foreach (var localVariable in operation.LocalVariables)
            {
                localVariable.DependentVariables
                    .RemoveWhere(x => !operationVariables.Contains(x) && !GlobalState.Variables.Contains(x));
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