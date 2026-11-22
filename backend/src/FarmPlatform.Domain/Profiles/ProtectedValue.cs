namespace FarmPlatform.Domain.Profiles;

/// <summary>
/// The stored form of a sensitive value, produced by the application layer's protector.
/// </summary>
/// <param name="Ciphertext">Versioned, authenticated ciphertext (never the plain value).</param>
/// <param name="BlindIndex">Keyed hash for equality lookups/uniqueness; empty if not needed.</param>
/// <param name="Last4">Last four characters, for masked display.</param>
public sealed record ProtectedValue(string Ciphertext, string BlindIndex, string Last4);
