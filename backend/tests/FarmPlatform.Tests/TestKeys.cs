using System.Security.Cryptography;

namespace FarmPlatform.Tests;

/// <summary>Throwaway encryption keys generated per test run.</summary>
internal static class TestKeys
{
    public static readonly string DataKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public static readonly string BlindIndexKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
