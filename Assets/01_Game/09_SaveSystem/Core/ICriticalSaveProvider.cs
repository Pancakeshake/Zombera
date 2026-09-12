using System;

namespace Zombera.Systems
{
    public interface ICriticalSaveProvider : ISaveProvider { }

    public sealed class CriticalSaveProviderException : Exception
    {
        public Type ProviderType { get; }

        public CriticalSaveProviderException(Type providerType, string operation, Exception inner)
            : base(BuildMessage(providerType, operation), inner)
        {
            ProviderType = providerType;
        }

        private static string BuildMessage(Type providerType, string operation)
        {
            var providerName = providerType != null ? providerType.FullName : "UnknownProvider";
            var operationName = string.IsNullOrWhiteSpace(operation) ? "operation" : operation;
            return $"Critical save provider {providerName} failed during {operationName}.";
        }
    }
}
