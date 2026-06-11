using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Blurt.Core;

/// <summary>
/// The real <see cref="ISecretProtector"/>: Windows DPAPI in current-user scope.
/// The ciphertext is decryptable only by the same Windows user on the same
/// machine, which is exactly the guarantee the design contract asks for the API
/// key. Lives in Core (not the app layer) because DPAPI runs headless in the
/// test process, so its round-trip is an automated unit test — unlike the Win32
/// hook / NAudio / SendInput adapters that need real hardware and stay in the
/// app layer.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    public byte[] Protect(byte[] plaintext)
        => ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] ciphertext)
        => ProtectedData.Unprotect(ciphertext, optionalEntropy: null, DataProtectionScope.CurrentUser);
}
