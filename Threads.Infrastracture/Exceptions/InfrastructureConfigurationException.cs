namespace Threads.Infrastracture.Exceptions;

public sealed class InfrastructureConfigurationException(string configurationKey)
    : Exception($"Required configuration key '{configurationKey}' is missing.")
{
    public string ConfigurationKey { get; } = configurationKey;
}
