using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Enums;
using Leyeco2Dashboard.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Leyeco2Dashboard.Services.Validators
{
    /// <summary>
    /// Stateless validation rules for the Transaction Monitor's own accounts. Registered as a singleton, so it
    /// holds no instance state: every input flows through method parameters. Throws only
    /// <see cref="UserValidationException"/>.
    /// </summary>
    public partial class UserValidator
    {
        public const int UsernameMinLength = 3;
        public const int UsernameMaxLength = 50;
        public const int NameMaxLength = 50;
        public const int EmailMaxLength = 256;
        public const int PasswordMinLength = 8;
        public const int PasswordMaxLength = 128;

        [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
        private static partial Regex EmailPattern();

        public void ValidateActor(bool actorExists)
        {
            if (!actorExists)
                throw new UserValidationException("Invalid user performing this action.");
        }

        /// <summary>
        /// An account's name, username and e-mail, already trimmed. <paramref name="userId"/> is
        /// the account being changed, left out of the uniqueness checks; null for a new one.
        /// </summary>
        public async Task ValidateProfileAsync(
            Guid? userId,
            string username,
            string firstName,
            string lastName,
            string email,
            IQueryable<User> users,
            CancellationToken cancellationToken = default)
        {
            Require(firstName, "First name", NameMaxLength);
            Require(lastName, "Last name", NameMaxLength);
            Require(username, "Username", UsernameMaxLength);

            if (username.Length < UsernameMinLength)
                throw new UserValidationException($"Username must be at least {UsernameMinLength} characters.");

            if (username.Any(char.IsWhiteSpace))
                throw new UserValidationException("Username can't contain spaces.");

            if (email.Length > EmailMaxLength)
                throw new UserValidationException($"Email exceeds the maximum length of {EmailMaxLength} characters.");

            if (email.Length > 0 && !EmailPattern().IsMatch(email))
                throw new UserValidationException("Email is not a valid email address.");

            // The database compares without case, as its unique index on Username does.
            if (await users.AnyAsync(u => u.Username == username && u.UserId != userId, cancellationToken))
                throw new UserValidationException("Username is already in use.");

            if (email.Length > 0 && await users.AnyAsync(u => u.Email == email && u.UserId != userId, cancellationToken))
                throw new UserValidationException("Email is already in use.");
        }

        public void ValidatePassword(string? password, string username)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new UserValidationException("Password is required.");

            if (password.Length < PasswordMinLength)
                throw new UserValidationException($"Password must be at least {PasswordMinLength} characters.");

            if (password.Length > PasswordMaxLength)
                throw new UserValidationException($"Password exceeds the maximum length of {PasswordMaxLength} characters.");

            if (!password.Any(char.IsAsciiLetterUpper) || !password.Any(char.IsAsciiLetterLower) || !password.Any(char.IsAsciiDigit))
                throw new UserValidationException("Password must include an upper-case letter, a lower-case letter and a number.");

            if (string.Equals(password, username, StringComparison.OrdinalIgnoreCase))
                throw new UserValidationException("Password can't be the username.");
        }

        /// <summary>
        /// Reads grant names into the Transaction Monitor's grants. Holding All access is holding every
        /// other grant, so a set that names it comes back as that one grant alone.
        /// </summary>
        public List<PermissionEnum> ValidateGrants(IEnumerable<string>? grants)
        {
            var parsed = new List<PermissionEnum>();

            foreach (var name in grants ?? [])
            {
                // Enum.TryParse also accepts "1101" and "A, B"; only a member's own name counts.
                if (!Enum.TryParse<PermissionEnum>(name, ignoreCase: false, out var permission)
                    || !string.Equals(permission.ToString(), name, StringComparison.Ordinal)
                    || permission.GetAccessedSystem() != AccessedSystemEnum.TransactionMonitor
                    || permission.IsReserved())
                {
                    throw new UserValidationException($"“{name}” is not an access the Transaction Monitor offers.");
                }

                if (!parsed.Contains(permission))
                    parsed.Add(permission);
            }

            if (parsed.Contains(PermissionEnum.TXM_Access))
                return [PermissionEnum.TXM_Access];

            parsed.Sort();
            return parsed;
        }

        private static void Require(string value, string field, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new UserValidationException($"{field} is required.");

            if (value.Length > maxLength)
                throw new UserValidationException($"{field} exceeds the maximum length of {maxLength} characters.");
        }
    }
}
