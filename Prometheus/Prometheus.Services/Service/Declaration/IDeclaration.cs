namespace Prometheus.Services.Service
{
    public interface IDeclaration
    {
        int Index { get; }
        string ApplyOn(string text);
    }
}