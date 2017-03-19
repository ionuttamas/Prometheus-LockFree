using System;
using System.Collections.Generic;
using System.Linq;
using Prometheus.Common;
using Prometheus.Services.Extensions;
using Prometheus.Services.Service;

namespace Prometheus.Services.Model
{
    public class DataStructure
    {
        private readonly Dictionary<string, List<MethodRegion>> _methodInternalCodes;

        public DataStructure()
        {
            GlobalVariables = new List<Variable>();
            Operations = new List<Method>();
            Structures = new List<Structure>();
            _methodInternalCodes = new Dictionary<string, List<MethodRegion>>();
        }

        public List<Structure> Structures { get; set; }
        public List<Variable> GlobalVariables { get; set; }
        public List<Method> Operations { get; set; }

        public Method this[string name]
        {
            get { return Operations.FirstOrDefault(x => x.Name == name); }
        }

        public bool HasGlobalVariable(string name)
        {
            return GlobalVariables.Any(x => x.Name == name);
        }

        public Variable GetGlobalVariable(string name)
        {
            return GlobalVariables.FirstOrDefault(x => x.Name == name);
        }

        public int GetRegionCode(string method, int index)
        {
            return _methodInternalCodes[method].First(x => x.Start <= index && index < x.End).Code;
        }

        public void AddStructure(Structure structure)
        {
            if (Structures.Any(x => x.Name == structure.Name))
                return;

            Structures.Add(structure);
        }

        public void AddGlobalVariable(Variable variable)
        {
            GlobalVariables.Add(variable);
        }

        public void AddOperation(Method method)
        {
            if (this[method.Name] != null)
                return;

            Operations.Add(method);
        }

        public void PostProcess()
        {
            ProcessDependencies();
            ExtractRegions();
        }

        private void ProcessDependencies()
        {
            foreach (var operation in Operations)
            {
                ProcessOperation(operation);
            }
        }

        private void ProcessOperation(Method method)
        {
            List<string> operationVariables = GlobalVariables
                .Select(x=>x.Name)
                .Except(method.LocalVariables.Select(x => x.Name))
                .ToList();

            foreach (var localVariable in method.LocalVariables)
            {
                localVariable.DependentVariables
                    .RemoveWhere(x => !operationVariables.Contains(x) && !HasGlobalVariable(x));
                localVariable.DependentVariables
                    .RemoveWhere(x => x == localVariable.Name);
            }

            foreach (var localVariable in method.LocalVariables)
            {
                if (localVariable.DependentVariables.Any(HasGlobalVariable))
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
                _methodInternalCodes[operation.Name] = ExtractOperationRegions(operation)
                    .Select(x=>new MethodRegion(x.Key,x.Value, codeCounter++))
                    .ToList();
            }
        }

        private Dictionary<int, int> ExtractOperationRegions(Method method)
        {
            var result = new Dictionary<int,int>();

            if (method.IfStatements.IsNullOrEmpty()) {
                result[method.StartIndex] = method.EndIndex;
                return result;
            }

            var regions = method
                    .IfStatements
                    .SelectMany(GetSelectionRegions)
                    .OrderBy(x => x.StartBodyIndex)
                    .ToList();

            result[method.StartIndex] = regions[0].StartStatementIndex;
            result[regions.Last().EndBodyIndex] = method.EndIndex;

            foreach (var region in regions)
            {
                result[region.StartBodyIndex] = region.EndBodyIndex;
            }

            for (int i = 1; i < method.IfStatements.Count; i++)
            {
                result[method.IfStatements[i-1].EndIndex] = method.IfStatements[i].StartIndex;
            }

            return result;
        }

        private List<SelectionRegion> GetSelectionRegions(IfStatement statement) {
            var result = new List<SelectionRegion>();

            if (statement == null)
                return result;

            if (statement.IfStatements.IsNullOrEmpty() && statement.ElseStatements.IsNullOrEmpty()) {
                result.Add(new SelectionRegion(statement.StartIndex, statement.Context.statement()[0].GetStartIndex(), statement.EndIndex));

                return result;
            }

            var regions = statement
                .IfStatements
                .SelectMany(GetSelectionRegions)
                .OrderBy(x => x.StartBodyIndex)
                .ToList();

            if (regions.Count > 0)
            {
                result.Add(new SelectionRegion(statement.StartIndex, statement.Context.statement()[0].compoundStatement().GetStartIndex(), regions[0].StartStatementIndex));
                result.Add(new SelectionRegion(-1, regions.Last().EndBodyIndex, statement.EndIndex));
                result.AddRange(regions);

                for (int i = 1; i < statement.IfStatements.Count; i++) {
                    result.Add(new SelectionRegion(-1, statement.IfStatements[i - 1].EndIndex, statement.IfStatements[i].StartIndex));
                }
            }
            else
            {
                result.Add(new SelectionRegion(statement.StartIndex, statement.Context.statement()[0].compoundStatement().GetStartIndex(), statement.Context.statement()[0].compoundStatement().GetStopIndex()));
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
                result.Add(new SelectionRegion(statement.StartIndex, statement.Context.GetStartIndex(), statement.EndIndex));

                return result;
            }

            var regions = statement
                .IfStatements
                .SelectMany(GetSelectionRegions)
                .OrderBy(x => x.StartBodyIndex)
                .ToList();

            if (regions.Count > 0) {
                result.Add(new SelectionRegion(statement.StartIndex, statement.Context.GetStartIndex(), regions[0].StartStatementIndex));
                result.Add(new SelectionRegion(-1, regions.Last().EndBodyIndex, statement.EndIndex));
                result.AddRange(regions);

                for (int i = 1; i < statement.IfStatements.Count; i++) {
                    result.Add(new SelectionRegion(-1, statement.IfStatements[i - 1].EndIndex, statement.IfStatements[i].StartIndex));
                }
            }
            else
            {
                result.Add(new SelectionRegion (statement.StartIndex, statement.Context.GetStartIndex(), statement.EndIndex));
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
                //todo: dependentVariables can contain tokens like "head->next" and these need to be treated separately
                Variable dependentVariable = this[variable.Operation][dependentVariableName];

                if(dependentVariable==null)
                    continue;

                LinkToGlobalState(dependentVariable);
            }
        }

        private class SelectionRegion
        {
            private readonly Interval _bodyInterval;

            public int StartStatementIndex { get; set; }
            public int StartBodyIndex => _bodyInterval.Start;
            public int EndBodyIndex => _bodyInterval.End;

            public SelectionRegion(int startStatement, int startBody, int endBody) {
                _bodyInterval = new Interval(startBody, endBody);
                StartStatementIndex = startStatement;
            }
        }

        private class MethodRegion
        {
            private readonly Interval _interval;

            public int Start => _interval.Start;
            public int End => _interval.End;
            public int Code { get; }

            public MethodRegion(int start, int end, int code)
            {
                _interval = new Interval(start, end);
                Code = code;
            }
        }
    }
}