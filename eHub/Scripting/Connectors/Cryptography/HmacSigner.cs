using System.Security.Cryptography;

namespace eHub.Scripting.Connectors.Cryptography;

/// <summary>
/// Provides functionality for signing and verifying data using HMACSHA256.
/// </summary>
public class HmacSigner : IDisposable
{
    private readonly HMACSHA256 _hmac;

    /// <summary>
    /// Initializes the signer.
    /// </summary>
    public HmacSigner()
    {
        _hmac = new HMACSHA256();
    }

    /// <summary>
    /// Signs the provided data and returns the signature as a byte array.
    /// </summary>
    /// <param name="data">The data to sign.</param>
    /// <returns>The HMAC signature as a byte array.</returns>
    public byte[] Sign(byte[] data) => _hmac.ComputeHash(data);

    /// <summary>
    /// Signs the provided data and returns the signature as a Base64-encoded string.
    /// </summary>
    /// <param name="data">The data to sign.</param>
    /// <returns>The HMAC signature as a Base64-encoded string.</returns>
    public string SignToBase64(byte[] data) => Convert.ToBase64String(Sign(data));

    /// <summary>
    /// Verifies the HMAC signature for the given data.
    /// </summary>
    /// <param name="data">The original data.</param>
    /// <param name="signature">The signature to verify.</param>
    /// <returns><c>true</c> if the signature is valid; otherwise, <c>false</c>.</returns>
    public bool Verify(byte[] data, byte[] signature)
    {
        byte[] computedSignature = _hmac.ComputeHash(data);
        return computedSignature.Length == signature.Length && computedSignature.AsSpan().SequenceEqual(signature);
    }

    /// <summary>
    /// Verifies the HMAC signature for the given data using a Base64-encoded signature.
    /// </summary>
    /// <param name="data">The original data.</param>
    /// <param name="base64Signature">The Base64-encoded signature to verify.</param>
    /// <returns><c>true</c> if the signature is valid; otherwise, <c>false</c>.</returns>
    public bool VerifyFromBase64(byte[] data, string base64Signature) => Verify(data, Convert.FromBase64String(base64Signature));

    public void Dispose()
    {
        _hmac.Dispose();
    }
}
