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
                                  x => new Dictionary<int, int> {{x.Value, codeCounter++}});
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
                    .SelectMany(GetSelectionRegions)
                    .OrderBy(x => x.StartBodyIndex)
                    .ToList();

            result[operation.StartIndex] = regions[0].StartStatementIndex;
            result[regions.Last().EndBodyIndex] = operation.EndIndex;

            foreach (var region in regions)
            {
                result[region.StartBodyIndex] = region.EndBodyIndex;
            }

            for (int i = 1; i < operation.IfStatements.Count; i++)
            {
                result[operation.IfStatements[i-1].EndIndex] = operation.IfStatements[i].StartIndex;
            }

            return result;
        }

        private List<SelectionRegion> GetSelectionRegions(IfStatement statement) {
            var result = new List<SelectionRegion>();

            if (statement == null)
                return result;

            if (statement.IfStatements.IsNullOrEmpty() && statement.ElseStatements.IsNullOrEmpty()) {
                result.Add(new SelectionRegion
                {
                    StartStatementIndex = statement.StartIndex,
                    StartBodyIndex = statement.Context.statement()[0].GetStartIndex(),
                    EndBodyIndex = statement.EndIndex
                });

                return result;
            }

            var regions = statement
                .IfStatements
                .SelectMany(GetSelectionRegions)
                .OrderBy(x => x.StartBodyIndex)
                .ToList();

            if (regions.Count > 0)
            {
                result.Add(new SelectionRegion
                {
                    StartStatementIndex = statement.StartIndex,
                    StartBodyIndex = statement.Context.statement()[0].compoundStatement().GetStartIndex(),
                    EndBodyIndex = regions[0].StartStatementIndex
                });

                result.Add(new SelectionRegion
                {
                    StartStatementIndex = -1,
                    StartBodyIndex = regions.Last().EndBodyIndex,
                    EndBodyIndex = statement.EndIndex
                });

                result.AddRange(regions);

                for (int i = 1; i < statement.IfStatements.Count; i++) {
                    result.Add(new SelectionRegion {
                        StartStatementIndex = -1,
                        StartBodyIndex = statement.IfStatements[i - 1].EndIndex,
                        EndBodyIndex = statement.IfStatements[i].StartIndex
                    });
                }
            }
            else
            {
                result.Add(new SelectionRegion {
                    StartStatementIndex = statement.StartIndex,
                    StartBodyIndex = statement.Context.statement()[0].compoundStatement().GetStartIndex(),
                    EndBodyIndex = statement.Context.statement()[0].compoundStatement().GetStopIndex()
                });
            }

            result.AddRange(statement.ElseStatements.SelectMany(GetSelectionRegions));

            return result;
        }

        private List<SelectionRegion> GetSelectionRegions(ElseStatement statement)
        {
            var result = new List<SelectionRegion>();

            if (statement == null)
                return result;

            if (statement.IfStatements.IsNullOrEmpty()) {
                result.Add(new SelectionRegion {
                    StartStatementIndex = statement.StartIndex,
                    StartBodyIndex = statement.Context.GetStartIndex(),
                    EndBodyIndex = statement.EndIndex
                });

                return result;
            }

            var regions = statement
                .IfStatements
                .SelectMany(GetSelectionRegions)
                .OrderBy(x => x.StartBodyIndex)
                .ToList();

            if (regions.Count > 0) {
                result.Add(new SelectionRegion {
                    StartStatementIndex = statement.StartIndex,
                    StartBodyIndex = statement.Context.GetStartIndex(),
                    EndBodyIndex = regions[0].StartStatementIndex
                });

                result.Add(new SelectionRegion {
                    StartStatementIndex = -1,
                    StartBodyIndex = regions.Last().EndBodyIndex,
                    EndBodyIndex = statement.EndIndex
                });

                result.AddRange(regions);

                for (int i = 1; i < statement.IfStatements.Count; i++) {
                    result.Add(new SelectionRegion {
                        StartStatementIndex = -1,
                        StartBodyIndex = statement.IfStatements[i - 1].EndIndex,
                        EndBodyIndex = statement.IfStatements[i].StartIndex
                    });
                }
            }
            else
            {
                result.Add(new SelectionRegion {
                    StartStatementIndex = statement.StartIndex,
                    StartBodyIndex = statement.Context.GetStartIndex(),
                    EndBodyIndex = statement.EndIndex
                });
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

        private class SelectionRegion
        {
            public int StartStatementIndex { get; set; }
            public int StartBodyIndex { get; set; }
            public int EndBodyIndex { get; set; }
        }
    }
}