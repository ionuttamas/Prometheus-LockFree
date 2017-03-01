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
    }
}