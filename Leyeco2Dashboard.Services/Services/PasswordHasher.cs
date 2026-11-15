using Leyeco2Dashboard.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// PBKDF2-HMAC-SHA512 with a random salt per password. The stored string names its own
    /// settings (<c>PBKDF2-SHA512$210000$salt$hash</c>, salt and hash in base64), so the iteration
    /// count can be raised later: an older hash still verifies, and <see cref="NeedsRehash"/> has
    /// the next sign-in replace it.
    /// </summary>
    public class PasswordHasher : IPasswordHasher
    {
        private const string Scheme = "PBKDF2-SHA512";
        private const int Iterations = 210_000;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        private static readonly byte[] NoSalt = new byte[SaltBytes];

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var hash = Derive(password, salt, Iterations, HashBytes);
            return string.Join('$', Scheme, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }

        public bool Verify(string password, string? storedHash)
        {
            if (string.IsNullOrEmpty(password) || !TryParse(storedHash, out var iterations, out var salt, out var expected))
                return false;

            var actual = Derive(password, salt, iterations, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        public bool NeedsRehash(string storedHash)
            => !TryParse(storedHash, out var iterations, out var salt, out var hash)
               || iterations < Iterations
               || salt.Length < SaltBytes
               || hash.Length < HashBytes;

        public void VerifyAgainstNothing(string password)
            => Derive(password ?? string.Empty, NoSalt, Iterations, HashBytes);

        private static byte[] Derive(string password, byte[] salt, int iterations, int length)
            => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA512, length);

        private static bool TryParse(string? storedHash, out int iterations, out byte[] salt, out byte[] hash)
        {
            iterations = 0;
            salt = [];
            hash = [];

            var parts = storedHash?.Split('$');
            if (parts is not { Length: 4 } || parts[0] != Scheme)
                return false;

            if (!int.TryParse(parts[1], out iterations) || iterations <= 0)
                return false;

            try
            {
                salt = Convert.FromBase64String(parts[2]);
                hash = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            return salt.Length > 0 && hash.Length > 0;
        }
    }
}
