using System.Security.Cryptography;

namespace Leyeco2Dashboard.Client.Infrastructure.Auth
{
    /// <summary>
    /// The API's password rule (<c>UserValidator.ValidatePassword</c>), checked here first so a
    /// form says what is wrong before it sends anything. The API still decides.
    /// </summary>
    public static class PasswordRules
    {
        public const int MinLength = 8;
        public const int MaxLength = 128;

        public const string Hint = "At least 8 characters, with an upper-case letter, a lower-case letter and a number.";

        // No look-alikes (0 O, 1 l I), since a first password is read aloud or copied by hand.
        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Lower = "abcdefghijkmnpqrstuvwxyz";
        private const string Digits = "23456789";

        /// <summary>What keeps <paramref name="password"/> from being accepted, or null when nothing does.</summary>
        public static string? Check(string? password, string? username = null)
        {
            if (string.IsNullOrWhiteSpace(password))
                return "Enter a password.";

            if (password.Length < MinLength)
                return $"Use at least {MinLength} characters.";

            if (password.Length > MaxLength)
                return $"Use {MaxLength} characters or fewer.";

            if (!password.Any(char.IsAsciiLetterUpper) || !password.Any(char.IsAsciiLetterLower) || !password.Any(char.IsAsciiDigit))
                return "Include an upper-case letter, a lower-case letter and a number.";

            if (!string.IsNullOrEmpty(username) && string.Equals(password, username.Trim(), StringComparison.OrdinalIgnoreCase))
                return "The password can't be the username.";

            return null;
        }

        /// <summary>A random first password that meets the rule: fourteen characters in three dashed groups.</summary>
        public static string Generate()
        {
            var all = Upper + Lower + Digits;
            var characters = new List<char>
            {
                Pick(Upper), Pick(Lower), Pick(Digits)
            };

            while (characters.Count < 12)
                characters.Add(Pick(all));

            // Fisher-Yates, so the three guaranteed characters do not always lead.
            for (var i = characters.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (characters[i], characters[j]) = (characters[j], characters[i]);
            }

            var text = new string(characters.ToArray());
            return $"{text[..4]}-{text[4..8]}-{text[8..]}";
        }

        private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }
}
