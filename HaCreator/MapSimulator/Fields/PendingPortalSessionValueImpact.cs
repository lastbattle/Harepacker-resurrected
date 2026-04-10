using System;

namespace HaCreator.MapSimulator
{
    internal sealed class PendingPortalSessionValueImpact
    {
        public PendingPortalSessionValueImpact(string key, string value, double velocityX, double velocityY)
        {
            Key = key?.Trim();
            Value = value ?? string.Empty;
            VelocityX = velocityX;
            VelocityY = velocityY;
        }

        public string Key { get; }

        public string Value { get; }

        public double VelocityX { get; }

        public double VelocityY { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Key);

        public bool Matches(string key, string value)
        {
            return IsMatch(Key, Value, key, value);
        }

        internal static bool IsMatch(string expectedKey, string expectedValue, string actualKey, string actualValue)
        {
            return string.Equals(expectedKey?.Trim(), actualKey?.Trim(), StringComparison.Ordinal)
                && string.Equals(expectedValue ?? string.Empty, actualValue ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
