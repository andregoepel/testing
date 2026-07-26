using OtpNet;

namespace AndreGoepel.Testing.E2E;

/// <summary>Generates authenticator codes from the shared key an EnableAuthenticator-style page displays.</summary>
public static class Totp
{
    /// <summary>
    /// Computes the current 6-digit code for a base32 shared key. UIs typically show the key in
    /// space-separated groups, so whitespace is stripped and the value upper-cased first.
    /// </summary>
    public static string Compute(string sharedKey)
    {
        var totp = new OtpNet.Totp(Base32Encoding.ToBytes(NormalizeSharedKey(sharedKey)));
        return totp.ComputeTotp();
    }

    /// <summary>Strips whitespace and upper-cases a base32 key as displayed by a typical enable-authenticator page.</summary>
    internal static string NormalizeSharedKey(string sharedKey) =>
        sharedKey.Replace(" ", string.Empty).ToUpperInvariant();
}
