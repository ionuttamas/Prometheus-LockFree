using System.Collections.Generic;
using System.Linq;
using Prometheus.Common;
using Prometheus.Services.Model;

namespace Prometheus.Services.Service
{
    public class OperationService
    {
        private readonly TypeService _typeService;

        public OperationService(TypeService typeService)
        {
            _typeService = typeService;
        }

        public Operation GetOperation(Structure structure)
        {
            Dictionary<string, string> members = structure.Fields
               .Where(x => _typeService.IsPointer(x.Type))
               .ToDictionary(x => x.Type, x => $"expected{x.Name.Capitalize()}");
            members.Add($"{structure.Name }*", $"expected{structure.Name.Capitalize()}");

            var operation = new Operation
            {
                Name = $"operation_{structure.Name}",
                Structure = structure,
                Members = members
            };

            return operation;
        }

        public string GetHelpMethod(Structure structure) {
            var argument = $"{structure.Name}Instance";
            var expectedArgument = $"expected{structure.Name.Capitalize()}";
            //We just assign the expected argument to the current argument;
            //The method that manages to set the "operation" field on the argument via CAS instruction is the "owner" of the argument modification
            var functionDeclaration = $"void Help({structure.Name} * {argument}){{" +
                                            $"{argument} = {argument}.{expectedArgument};" +
                                      $"}}";

            return functionDeclaration;
        }
    }
}