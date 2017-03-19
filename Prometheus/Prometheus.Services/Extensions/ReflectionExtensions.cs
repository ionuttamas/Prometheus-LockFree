using System.Reflection;

namespace Prometheus.Services.Extensions
{
    public static class ReflectionExtensions
    {
        public static object GetValue(this object value, string propertyName) {
            PropertyInfo property = value
                .GetType()
                .GetProperty(propertyName);

            object result = property?.GetValue(value);

            return result;
        }

        public static object Invoke(this object value, string function, params object[] args) {
            MethodInfo method = value
                .GetType()
                .GetMethod(function);

            object result = method.Invoke(value, args);

            return result;
        }


    }
}