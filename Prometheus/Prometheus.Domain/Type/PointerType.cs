namespace Prometheus.Domain.Type
{
    public class PointerType : IType
    {
        /// <summary>
        /// Pointers to struct types are handled differently, allowing only pointers to primitive types.
        /// </summary>
        public PrimitiveType PrimitiveType { get; set; }
    }
}