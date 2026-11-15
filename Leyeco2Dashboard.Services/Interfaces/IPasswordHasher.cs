namespace Leyeco2Dashboard.Services.Interfaces
{
    /// <summary>Turns a password into the string <c>UserCredentials.PasswordHash</c> stores, and checks one against it.</summary>
    public interface IPasswordHasher
    {
        string Hash(string password);

        /// <summary>False for a wrong password and for a stored value this build cannot read.</summary>
        bool Verify(string password, string? storedHash);

        /// <summary>The stored hash was made with weaker settings than today's: hash the password again at the next sign-in.</summary>
        bool NeedsRehash(string storedHash);

        /// <summary>
        /// Spends the time a real check takes, for a sign-in that names no account, so its answer
        /// does not come back sooner than a wrong password's.
        /// </summary>
        void VerifyAgainstNothing(string password);
    }
}
