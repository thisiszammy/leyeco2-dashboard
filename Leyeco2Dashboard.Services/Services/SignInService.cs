using Leyeco2Dashboard.Data;
using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Enums;
using Leyeco2Dashboard.Data.Interfaces;
using Leyeco2Dashboard.Services.Configuration;
using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Interfaces;
using Leyeco2Dashboard.Services.Validators;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// Sign-in against the Transaction Monitor's own accounts. A session is one row of <c>UserSessions</c> holding
    /// the SHA-256 of a random token; the token itself lives only in the caller's refresh cookie.
    /// The token is not rotated: a refresh pushes the session's end back and reads the account and
    /// its grants again, so a deactivation ends a session and a changed grant reaches it at its
    /// next refresh.
    /// </summary>
    public class SignInService : ISignInService
    {
        // Dead sessions are kept this long for whoever is tracing a sign-in, then go at the
        // account's next one.
        private const int DeadSessionRetentionDays = 30;

        private const int IpAddressMaxLength = 40;
        private const int UserAgentMaxLength = 200;

        private readonly IUserRepository _userRepository;
        private readonly IUserCredentialRepository _credentialRepository;
        private readonly IUserGrantRepository _grantRepository;
        private readonly IUserSessionRepository _sessionRepository;
        private readonly IActivityLogRepository _activityLogRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDateTimeService _dateTime;
        private readonly IClientContextAccessor _clientContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly UserValidator _validator;
        private readonly CredentialOptions _options;

        public SignInService(
            IUserRepository userRepository,
            IUserCredentialRepository credentialRepository,
            IUserGrantRepository grantRepository,
            IUserSessionRepository sessionRepository,
            IActivityLogRepository activityLogRepository,
            IUnitOfWork unitOfWork,
            IDateTimeService dateTime,
            IClientContextAccessor clientContext,
            IPasswordHasher passwordHasher,
            UserValidator validator,
            CredentialOptions options)
        {
            _userRepository = userRepository;
            _credentialRepository = credentialRepository;
            _grantRepository = grantRepository;
            _sessionRepository = sessionRepository;
            _activityLogRepository = activityLogRepository;
            _unitOfWork = unitOfWork;
            _dateTime = dateTime;
            _clientContext = clientContext;
            _passwordHasher = passwordHasher;
            _validator = validator;
            _options = options;
        }

        public async Task<SessionDTO> SignInAsync(
            string username,
            string password,
            bool persistent,
            CancellationToken cancellationToken = default)
        {
            var user = await VerifyAsync(username, password, cancellationToken);

            if (user.UserCredential.MustChangePassword)
                throw new SignInException(SignInFailureKind.PasswordChangeRequired, "Set a new password to finish signing in.");

            return await OpenSessionAsync(user, password, persistent, cancellationToken);
        }

        public async Task<SessionDTO> RefreshAsync(string sessionToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sessionToken))
                throw Ended();

            var hash = HashToken(sessionToken);
            var now = _dateTime.Now();

            var session = await _sessionRepository.GetUserSessions()
                .Include(s => s.User)
                .ThenInclude(u => u.UserCredential)
                .FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

            if (session == null || session.RevokedOn != null || session.ExpiresOn <= now)
                throw Ended();

            var credential = session.User.UserCredential;
            if (credential is not { IsActive: true, MustChangePassword: false })
            {
                session.RevokedOn = now;
                _sessionRepository.UpdateUserSession(session);
                await _unitOfWork.SaveChangesAsync<DatabaseContext>(cancellationToken);
                throw Ended();
            }

            session.LastUsedOn = now;
            session.ExpiresOn = now.Add(Lifetime(session.IsPersistent));
            _sessionRepository.UpdateUserSession(session);
            await _unitOfWork.SaveChangesAsync<DatabaseContext>(cancellationToken);

            var permissions = await GrantsAsync(session.UserId, cancellationToken);
            return ToSession(session.User, permissions, sessionToken, session);
        }

        public async Task SignOutAsync(string? sessionToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sessionToken))
                return;

            var hash = HashToken(sessionToken);
            var session = await _sessionRepository.GetUserSessions()
                .FirstOrDefaultAsync(s => s.TokenHash == hash, cancellationToken);

            if (session == null || session.RevokedOn != null)
                return;

            session.RevokedOn = _dateTime.Now();
            _sessionRepository.UpdateUserSession(session);
            await _unitOfWork.SaveChangesAsync<DatabaseContext>(cancellationToken);
        }

        public async Task<SessionDTO> ChangePasswordAsync(
            string username,
            string currentPassword,
            string newPassword,
            bool persistent,
            CancellationToken cancellationToken = default)
        {
            var user = await VerifyAsync(username, currentPassword, cancellationToken);
            var credential = user.UserCredential;

            _validator.ValidatePassword(newPassword, user.Username);

            if (_passwordHasher.Verify(newPassword, credential.PasswordHash))
                throw new UserValidationException("The new password must differ from the current one.");

            var timestamp = _dateTime.Now();

            credential.PasswordHash = _passwordHasher.Hash(newPassword);
            credential.PasswordChangedOn = timestamp;
            credential.MustChangePassword = false;
            credential.FailedSignInCount = 0;
            credential.LockedUntil = null;
            credential.UpdatedOn = timestamp;
            credential.UpdatedBy = user.UserId;

            // Whoever else holds a session of this account held it on the old password.
            var sessions = await _sessionRepository.GetUserSessions()
                .Where(s => s.UserId == user.UserId && s.RevokedOn == null && s.ExpiresOn > timestamp)
                .ToListAsync(cancellationToken);
            foreach (var session in sessions)
                session.RevokedOn = timestamp;

            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                ActivityEnum.ChangedOwnPassword,
                user.UserId,
                timestamp,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(ActivityEnum.ChangedOwnPassword, user.Username),
                ActivityLogReferenceTypeEnum.User,
                user.UserId.ToString());

            await PersistAsync(() =>
            {
                _credentialRepository.UpdateUserCredential(credential);
                foreach (var session in sessions)
                    _sessionRepository.UpdateUserSession(session);
                _activityLogRepository.AddActivityLog(activityLog);
            });

            return await OpenSessionAsync(user, newPassword, persistent, cancellationToken);
        }

        /// <summary>
        /// The account the username and password name, with its credentials loaded. A wrong
        /// password is counted, and the count that reaches the limit locks the account.
        /// </summary>
        private async Task<User> VerifyAsync(string username, string password, CancellationToken cancellationToken)
        {
            username = username?.Trim() ?? string.Empty;
            password ??= string.Empty;

            var user = username.Length == 0
                ? null
                : await _userRepository.GetUsers()
                    .Include(u => u.UserCredential)
                    .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

            var credential = user?.UserCredential;
            if (user == null || credential == null)
            {
                _passwordHasher.VerifyAgainstNothing(password);
                throw Invalid();
            }

            var now = _dateTime.Now();
            if (credential.LockedUntil > now)
                throw Locked(credential.LockedUntil.Value);

            if (!_passwordHasher.Verify(password, credential.PasswordHash))
            {
                var lockedUntil = await CountFailureAsync(user, credential, now);
                throw lockedUntil is DateTime until ? Locked(until) : Invalid();
            }

            // Told only to someone who knows the password.
            if (!credential.IsActive)
                throw new SignInException(SignInFailureKind.Deactivated,
                    "This account has been deactivated. Ask an administrator to reactivate it.");

            return user;
        }

        /// <summary>Counts one wrong password; answers when the lock it caused lapses, if it caused one.</summary>
        private async Task<DateTime?> CountFailureAsync(User user, UserCredential credential, DateTime now)
        {
            credential.FailedSignInCount++;

            if (credential.FailedSignInCount < _options.MaxFailedSignIns)
            {
                _credentialRepository.UpdateUserCredential(credential);
                await _unitOfWork.SaveChangesAsync<DatabaseContext>();
                return null;
            }

            var lockedUntil = now.AddMinutes(_options.LockoutMinutes);
            var failures = credential.FailedSignInCount;
            credential.FailedSignInCount = 0;
            credential.LockedUntil = lockedUntil;

            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                ActivityEnum.LockedUser,
                user.UserId,
                now,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(ActivityEnum.LockedUser, user.Username, Clock(lockedUntil), failures),
                ActivityLogReferenceTypeEnum.User,
                user.UserId.ToString());

            await PersistAsync(() =>
            {
                _credentialRepository.UpdateUserCredential(credential);
                _activityLogRepository.AddActivityLog(activityLog);
            });

            return lockedUntil;
        }

        private async Task<SessionDTO> OpenSessionAsync(User user, string password, bool persistent, CancellationToken cancellationToken)
        {
            var permissions = await GrantsAsync(user.UserId, cancellationToken);
            if (permissions.Count == 0)
                throw new SignInException(SignInFailureKind.NoAccess,
                    "This account has no access to the Transaction Monitor yet. Ask an administrator to give it access on the Users screen.");

            var timestamp = _dateTime.Now();
            var credential = user.UserCredential;
            var token = NewToken();

            var session = new UserSession
            {
                SessionId = Guid.NewGuid(),
                UserId = user.UserId,
                TokenHash = HashToken(token),
                IsPersistent = persistent,
                CreatedOn = timestamp,
                LastUsedOn = timestamp,
                ExpiresOn = timestamp.Add(Lifetime(persistent)),
                IpAddress = Fit(_clientContext.IpAddress, IpAddressMaxLength),
                UserAgent = Fit(_clientContext.UserAgent, UserAgentMaxLength)
            };

            credential.FailedSignInCount = 0;
            credential.LockedUntil = null;
            if (_passwordHasher.NeedsRehash(credential.PasswordHash))
                credential.PasswordHash = _passwordHasher.Hash(password);

            user.LastSignInOn = timestamp;

            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                ActivityEnum.SignedIn,
                user.UserId,
                timestamp,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(ActivityEnum.SignedIn, user.Username),
                ActivityLogReferenceTypeEnum.User,
                user.UserId.ToString());

            var deadBefore = timestamp.AddDays(-DeadSessionRetentionDays);
            var dead = await _sessionRepository.GetUserSessions()
                .Where(s => s.UserId == user.UserId && (s.ExpiresOn < deadBefore || s.RevokedOn < deadBefore))
                .ToListAsync(cancellationToken);

            await PersistAsync(() =>
            {
                _sessionRepository.GetUserSessions().RemoveRange(dead);
                _sessionRepository.AddUserSession(session);
                _credentialRepository.UpdateUserCredential(credential);
                _userRepository.UpdateUser(user);
                _activityLogRepository.AddActivityLog(activityLog);
            });

            return ToSession(user, permissions, token, session);
        }

        private async Task<List<PermissionEnum>> GrantsAsync(Guid userId, CancellationToken cancellationToken)
        {
            var held = await _grantRepository.GetUserGrants()
                .Where(g => g.UserId == userId)
                .Select(g => g.Permission)
                .ToListAsync(cancellationToken);

            return held
                .Select(p => (PermissionEnum)p)
                .Where(p => Enum.IsDefined(p) && p.GetAccessedSystem() == AccessedSystemEnum.TransactionMonitor && !p.IsReserved())
                .Distinct()
                .OrderBy(p => p)
                .ToList();
        }

        private static SessionDTO ToSession(User user, List<PermissionEnum> permissions, string token, UserSession session)
            => new()
            {
                UserId = user.UserId,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Permissions = permissions,
                SessionToken = token,
                ExpiresOn = session.ExpiresOn,
                IsPersistent = session.IsPersistent
            };

        private TimeSpan Lifetime(bool persistent)
            => persistent ? TimeSpan.FromDays(_options.KeptSessionDays) : TimeSpan.FromHours(_options.SessionHours);

        private static string NewToken()
            => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string HashToken(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        // ActivityLogs takes neither a null nor an over-long value, and a sign-in must not fail on
        // its audit row.
        private string ClientAddress => Fit(_clientContext.IpAddress, IpAddressMaxLength) ?? "unknown";

        private string ClientAgent => Fit(_clientContext.UserAgent, UserAgentMaxLength) ?? "unknown";

        private static string? Fit(string? value, int maxLength)
            => string.IsNullOrEmpty(value) ? null : value.Length <= maxLength ? value : value[..maxLength];

        private static string Clock(DateTime time) => time.ToString("h:mm tt", CultureInfo.InvariantCulture);

        private static SignInException Invalid()
            => new(SignInFailureKind.InvalidCredentials, "Incorrect username or password.");

        private static SignInException Locked(DateTime until)
            => new(SignInFailureKind.Locked,
                $"This account is locked after too many failed sign-ins. Try again after {Clock(until)}, or ask an administrator to unlock it.");

        private static SignInException Ended()
            => new(SignInFailureKind.SessionEnded, "Your session has ended. Sign in again to continue.");

        private async Task PersistAsync(Action writes)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync<DatabaseContext>();

                writes();

                await _unitOfWork.CommitAsync<DatabaseContext>();
            }
            catch
            {
                try { await _unitOfWork.RollbackAsync<DatabaseContext>(); }
                catch { }
                throw;
            }
        }
    }
}
