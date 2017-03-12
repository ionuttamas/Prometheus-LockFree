using System.Linq;
using Prometheus.Common;
using Prometheus.Services.Model;

namespace Prometheus.Services.Service {
    public class TypeService
    {
        private const string POINTER_ACCESS_MARKER = "->";
        private const string POINTER_MARKER = "*";
        private const string STRUCTURE_MARKER = "struct";
        private readonly DataStructure _dataStructure;

        public TypeService(DataStructure dataStructure)
        {
            _dataStructure = dataStructure;
        }

        public bool IsStructure(string value)
        {
            return value.Contains(STRUCTURE_MARKER);
        }

        public bool IsPointer(string type)
        {
            return type.Contains(POINTER_MARKER);
        }

        public string GetType(string expression, string operation) {
            if (!expression.ContainsInvariant(POINTER_ACCESS_MARKER)) {
                return GetSimpleVariableType(expression, operation);
            }

            return GetStructureVariableType(expression, operation);
        }

        private string GetSimpleVariableType(string expression, string operation) {
            Variable localVariable = _dataStructure[operation][expression];

            return localVariable != null ?
                    localVariable.Type :
                    _dataStructure.GetGlobalVariable(expression).Type;
        }

        private string GetStructureVariableType(string expression, string operation) {
            string[] referenceTokens = expression.Split(POINTER_ACCESS_MARKER);
            string currentType = GetSimpleVariableType(referenceTokens[0], operation);
            string trimmedType = currentType.TrimEnd(POINTER_MARKER).TrimEnd();
            Structure structure = _dataStructure.Structures.First(x => trimmedType.EndsWith(x.Name));

            foreach (var token in referenceTokens.Skip(1)) {
                currentType = structure[token].Type;
                trimmedType = structure[token].Type.TrimEnd(POINTER_MARKER).TrimEnd();

                if (IsStructure(currentType))
                {
                    structure = _dataStructure.Structures.First(x => trimmedType.EndsWith(x.Name));
                }
            }

            return currentType.Contains(POINTER_MARKER) ?
                $"{currentType.Substring(0, currentType.InvariantIndexOf(POINTER_MARKER))} {currentType.Substring(currentType.InvariantIndexOf(POINTER_MARKER))}" :
                currentType;
        }
    }
}
