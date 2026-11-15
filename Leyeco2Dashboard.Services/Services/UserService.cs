using Leyeco2Dashboard.Data;
using Leyeco2Dashboard.Data.Entities.Database;
using Leyeco2Dashboard.Data.Enums;
using Leyeco2Dashboard.Data.Interfaces;
using Leyeco2Dashboard.Services.DTOs;
using Leyeco2Dashboard.Services.Enums;
using Leyeco2Dashboard.Services.Exceptions;
using Leyeco2Dashboard.Services.Interfaces;
using Leyeco2Dashboard.Services.Validators;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Leyeco2Dashboard.Services.Services
{
    /// <summary>
    /// The Transaction Monitor's own staff accounts, following the <see cref="DepartmentService"/> canonical shape:
    /// primitive-only parameters, a stateless validator, an <see cref="ActivityLog"/> audit row
    /// paired with every write, and each write committed inside a transaction with rollback on
    /// failure. A password is only ever hashed here: it is never stored, logged or returned.
    /// </summary>
    public class UserService : IUserService
    {
        private const int MaxPageSize = 100;

        private const string NotFoundMessage = "User not found.";

        private const string LastAdministratorMessage =
            "At least one active account must be able to manage users. Give another account “Manage users” or “All access” first.";

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

        public UserService(
            IUserRepository userRepository,
            IUserCredentialRepository credentialRepository,
            IUserGrantRepository grantRepository,
            IUserSessionRepository sessionRepository,
            IActivityLogRepository activityLogRepository,
            IUnitOfWork unitOfWork,
            IDateTimeService dateTime,
            IClientContextAccessor clientContext,
            IPasswordHasher passwordHasher,
            UserValidator validator)
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
        }

        public async Task<UserPagedListDTO> GetUsersPagedAsync(
            string? search,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var now = _dateTime.Now();
            IQueryable<User> users = _userRepository.GetUsers();

            // The register's figures ignore the search and the filter.
            var totalAll = await users.CountAsync(cancellationToken);
            var activeAll = await users.CountAsync(IsActive(), cancellationToken);
            var attentionAll = await users.CountAsync(NeedsAttention(now), cancellationToken);
            var deactivatedAll = await users.CountAsync(IsDeactivated(), cancellationToken);

            var filtered = (status?.Trim().ToLowerInvariant()) switch
            {
                "active" => users.Where(IsActive()),
                "attention" => users.Where(NeedsAttention(now)),
                "deactivated" => users.Where(IsDeactivated()),
                _ => users,
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                // The full-name concat also covers plain first-name and last-name terms.
                var term = search.Trim().ToLower();
                filtered = filtered.Where(u =>
                    u.Username.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term) ||
                    (u.FirstName + " " + u.LastName).ToLower().Contains(term));
            }

            var totalCount = await filtered.CountAsync(cancellationToken);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var rows = await filtered
                .OrderBy(u => u.FirstName)
                .ThenBy(u => u.LastName)
                .ThenBy(u => u.Username)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(Row())
                .ToListAsync(cancellationToken);

            var grants = await GrantsOfAsync(rows.Select(r => r.UserId).ToList(), cancellationToken);

            return new UserPagedListDTO
            {
                Items = rows.Select(r => ToDto(r, grants, now)).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                TotalUsers = totalAll,
                ActiveUsers = activeAll,
                AttentionUsers = attentionAll,
                DeactivatedUsers = deactivatedAll
            };
        }

        public async Task<UserDTO> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var row = await _userRepository.GetUsers()
                .Where(u => u.UserId == userId)
                .Select(Row())
                .FirstOrDefaultAsync(cancellationToken);

            if (row == null)
                throw new UserValidationException(NotFoundMessage);

            var grants = await GrantsOfAsync([userId], cancellationToken);
            return ToDto(row, grants, _dateTime.Now());
        }

        public async Task<Guid> CreateUserAsync(
            string username,
            string firstName,
            string lastName,
            string? email,
            string password,
            bool mustChangePassword,
            IEnumerable<string> grants,
            Guid createdBy,
            CancellationToken cancellationToken = default)
        {
            username = Clean(username);
            firstName = Clean(firstName);
            lastName = Clean(lastName);
            email = Clean(email);

            var users = _userRepository.GetUsers();

            _validator.ValidateActor(await users.AnyAsync(u => u.UserId == createdBy, cancellationToken));
            await _validator.ValidateProfileAsync(null, username, firstName, lastName, email, users, cancellationToken);
            _validator.ValidatePassword(password, username);
            var permissions = _validator.ValidateGrants(grants);

            var batchId = Guid.NewGuid();
            var executionOrder = 0;
            var timestamp = _dateTime.Now();

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Username = username,
                FirstName = firstName,
                LastName = lastName,
                Email = email
            };

            var credential = NewCredential(user.UserId, password, mustChangePassword, timestamp, createdBy);
            var grantRows = permissions.Select(p => NewGrant(user.UserId, p, timestamp, createdBy)).ToList();

            var activityLog = ActivityLog.Create(
                batchId,
                ++executionOrder,
                ActivityEnum.CreatedUser,
                createdBy,
                timestamp,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(ActivityEnum.CreatedUser, username, $"{firstName} {lastName}"),
                ActivityLogReferenceTypeEnum.User,
                user.UserId.ToString(),
                toSignature: AccessSignature(permissions));

            await PersistAsync(() =>
            {
                _userRepository.AddUser(user);
                _credentialRepository.AddUserCredential(credential);
                foreach (var grant in grantRows)
                    _grantRepository.AddUserGrant(grant);
                _activityLogRepository.AddActivityLog(activityLog);
            });

            return user.UserId;
        }

        public async Task UpdateUserAsync(
            Guid userId,
            string username,
            string firstName,
            string lastName,
            string? email,
            Guid updatedBy,
            CancellationToken cancellationToken = default)
        {
            username = Clean(username);
            firstName = Clean(firstName);
            lastName = Clean(lastName);
            email = Clean(email);

            var users = _userRepository.GetUsers();

            _validator.ValidateActor(await users.AnyAsync(u => u.UserId == updatedBy, cancellationToken));

            var user = await users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
                ?? throw new UserValidationException(NotFoundMessage);

            await _validator.ValidateProfileAsync(userId, username, firstName, lastName, email, users, cancellationToken);

            var changes = new List<string>();
            if (user.Username != username) changes.Add("username");
            if (user.FirstName != firstName) changes.Add("first name");
            if (user.LastName != lastName) changes.Add("last name");
            if (user.Email != email) changes.Add("email");

            if (changes.Count == 0)
                return;

            var fromSignature = $"{user.Username} | {user.FirstName} {user.LastName} | {user.Email}";

            user.Username = username;
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;

            var timestamp = _dateTime.Now();
            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                ActivityEnum.UpdatedUser,
                updatedBy,
                timestamp,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(ActivityEnum.UpdatedUser, username, string.Join(", ", changes)),
                ActivityLogReferenceTypeEnum.User,
                userId.ToString(),
                fromSignature,
                $"{username} | {firstName} {lastName} | {email}");

            await PersistAsync(() =>
            {
                _userRepository.UpdateUser(user);
                _activityLogRepository.AddActivityLog(activityLog);
            });
        }

        public async Task SetUserAccessAsync(
            Guid userId,
            IEnumerable<string> grants,
            Guid updatedBy,
            CancellationToken cancellationToken = default)
        {
            var users = _userRepository.GetUsers();

            _validator.ValidateActor(await users.AnyAsync(u => u.UserId == updatedBy, cancellationToken));

            var user = await users.Include(u => u.UserCredential).FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
                ?? throw new UserValidationException(NotFoundMessage);

            var wanted = _validator.ValidateGrants(grants);

            var current = await _grantRepository.GetUserGrants()
                .Where(g => g.UserId == userId)
                .ToListAsync(cancellationToken);
            var held = current.Select(g => (PermissionEnum)g.Permission).ToList();

            if (ManagesUsers(held) && !ManagesUsers(wanted))
            {
                if (userId == updatedBy)
                    throw new UserValidationException("You can't remove your own access to the Users screen. Ask another administrator to change it.");

                if (user.UserCredential is { IsActive: true } && !await AnotherAdministratorExistsAsync(userId, cancellationToken))
                    throw new UserValidationException(LastAdministratorMessage);
            }

            var toRemove = current.Where(g => !wanted.Contains((PermissionEnum)g.Permission)).ToList();
            var toAdd = wanted.Where(p => !held.Contains(p)).ToList();

            if (toRemove.Count == 0 && toAdd.Count == 0)
                return;

            var timestamp = _dateTime.Now();
            var changes = new List<string>();
            if (toAdd.Count > 0)
                changes.Add("granted " + string.Join(", ", toAdd.Select(p => p.GetLabel())));
            if (toRemove.Count > 0)
                changes.Add("revoked " + string.Join(", ", toRemove.Select(g => ((PermissionEnum)g.Permission).GetLabel())));

            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                ActivityEnum.ChangedUserAccess,
                updatedBy,
                timestamp,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(ActivityEnum.ChangedUserAccess, user.Username, string.Join("; ", changes)),
                ActivityLogReferenceTypeEnum.User,
                userId.ToString(),
                AccessSignature(held),
                AccessSignature(wanted));

            await PersistAsync(() =>
            {
                foreach (var grant in toRemove)
                    _grantRepository.RemoveUserGrant(grant);
                foreach (var permission in toAdd)
                    _grantRepository.AddUserGrant(NewGrant(userId, permission, timestamp, updatedBy));
                _activityLogRepository.AddActivityLog(activityLog);
            });
        }

        public async Task SetUserActiveAsync(
            Guid userId,
            bool isActive,
            Guid updatedBy,
            CancellationToken cancellationToken = default)
        {
            var users = _userRepository.GetUsers();

            _validator.ValidateActor(await users.AnyAsync(u => u.UserId == updatedBy, cancellationToken));

            var user = await users.Include(u => u.UserCredential).FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
                ?? throw new UserValidationException(NotFoundMessage);

            var credential = user.UserCredential
                ?? throw new UserValidationException("This account has no password yet. Set one to let it sign in.");

            if (credential.IsActive == isActive)
                throw new UserValidationException(isActive ? "This account is not deactivated." : "This account is already deactivated.");

            var timestamp = _dateTime.Now();
            var sessions = new List<UserSession>();

            if (!isActive)
            {
                if (userId == updatedBy)
                    throw new UserValidationException("You can't deactivate your own account.");

                var held = await HeldAsync(userId, cancellationToken);
                if (ManagesUsers(held) && !await AnotherAdministratorExistsAsync(userId, cancellationToken))
                    throw new UserValidationException(LastAdministratorMessage);

                sessions = await OpenSessionsAsync(userId, timestamp, cancellationToken);
            }

            credential.IsActive = isActive;
            credential.FailedSignInCount = 0;
            credential.LockedUntil = null;
            credential.UpdatedOn = timestamp;
            credential.UpdatedBy = updatedBy;

            foreach (var session in sessions)
                session.RevokedOn = timestamp;

            var activity = isActive ? ActivityEnum.ReactivatedUser : ActivityEnum.DeactivatedUser;
            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                activity,
                updatedBy,
                timestamp,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(activity, user.Username, sessions.Count),
                ActivityLogReferenceTypeEnum.User,
                userId.ToString());

            await PersistAsync(() =>
            {
                _credentialRepository.UpdateUserCredential(credential);
                foreach (var session in sessions)
                    _sessionRepository.UpdateUserSession(session);
                _activityLogRepository.AddActivityLog(activityLog);
            });
        }

        public async Task ResetUserPasswordAsync(
            Guid userId,
            string newPassword,
            bool mustChangePassword,
            Guid updatedBy,
            CancellationToken cancellationToken = default)
        {
            var users = _userRepository.GetUsers();

            _validator.ValidateActor(await users.AnyAsync(u => u.UserId == updatedBy, cancellationToken));

            if (userId == updatedBy)
                throw new UserValidationException("Use Change password, beside your name in the sidebar, for your own account.");

            var user = await users.Include(u => u.UserCredential).FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
                ?? throw new UserValidationException(NotFoundMessage);

            _validator.ValidatePassword(newPassword, user.Username);

            var timestamp = _dateTime.Now();
            var credential = user.UserCredential;
            var isNew = credential == null;

            if (credential == null)
            {
                credential = NewCredential(userId, newPassword, mustChangePassword, timestamp, updatedBy);
            }
            else
            {
                credential.PasswordHash = _passwordHasher.Hash(newPassword);
                credential.PasswordChangedOn = timestamp;
                credential.MustChangePassword = mustChangePassword;
                credential.FailedSignInCount = 0;
                credential.LockedUntil = null;
                credential.UpdatedOn = timestamp;
                credential.UpdatedBy = updatedBy;
            }

            var sessions = await OpenSessionsAsync(userId, timestamp, cancellationToken);
            foreach (var session in sessions)
                session.RevokedOn = timestamp;

            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                ActivityEnum.ResetUserPassword,
                updatedBy,
                timestamp,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(ActivityEnum.ResetUserPassword, user.Username),
                ActivityLogReferenceTypeEnum.User,
                userId.ToString());

            await PersistAsync(() =>
            {
                if (isNew)
                    _credentialRepository.AddUserCredential(credential);
                else
                    _credentialRepository.UpdateUserCredential(credential);
                foreach (var session in sessions)
                    _sessionRepository.UpdateUserSession(session);
                _activityLogRepository.AddActivityLog(activityLog);
            });
        }

        public async Task UnlockUserAsync(Guid userId, Guid updatedBy, CancellationToken cancellationToken = default)
        {
            var users = _userRepository.GetUsers();

            _validator.ValidateActor(await users.AnyAsync(u => u.UserId == updatedBy, cancellationToken));

            var user = await users.Include(u => u.UserCredential).FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
                ?? throw new UserValidationException(NotFoundMessage);

            var timestamp = _dateTime.Now();
            var credential = user.UserCredential;

            if (credential == null || !(credential.LockedUntil > timestamp))
                throw new UserValidationException("This account is not locked.");

            credential.FailedSignInCount = 0;
            credential.LockedUntil = null;
            credential.UpdatedOn = timestamp;
            credential.UpdatedBy = updatedBy;

            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                ActivityEnum.UnlockedUser,
                updatedBy,
                timestamp,
                ClientAddress,
                ClientAgent,
                ActivityLog.BuildDetail(ActivityEnum.UnlockedUser, user.Username),
                ActivityLogReferenceTypeEnum.User,
                userId.ToString());

            await PersistAsync(() =>
            {
                _credentialRepository.UpdateUserCredential(credential);
                _activityLogRepository.AddActivityLog(activityLog);
            });
        }

        public async Task<AdministratorBootstrapEnum> EnsureAdministratorAsync(
            string? username,
            string? password,
            CancellationToken cancellationToken = default)
        {
            if (await AnotherAdministratorExistsAsync(Guid.Empty, cancellationToken))
                return AdministratorBootstrapEnum.AlreadyPresent;

            username = Clean(username);
            if (username.Length == 0 || string.IsNullOrEmpty(password))
                return AdministratorBootstrapEnum.NotConfigured;

            _validator.ValidatePassword(password, username);

            var timestamp = _dateTime.Now();
            var user = await _userRepository.GetUsers()
                .Include(u => u.UserCredential)
                .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

            var isNewUser = user == null;
            user ??= new User
            {
                UserId = Guid.NewGuid(),
                Username = username,
                FirstName = "System",
                LastName = "Administrator",
                Email = string.Empty
            };

            // The account is the actor of its own first rows: nobody else exists to be.
            var credential = user.UserCredential;
            var isNewCredential = credential == null;

            if (credential == null)
            {
                credential = NewCredential(user.UserId, password, mustChangePassword: true, timestamp, createdBy: null);
            }
            else
            {
                credential.PasswordHash = _passwordHasher.Hash(password);
                credential.PasswordChangedOn = timestamp;
                credential.MustChangePassword = true;
                credential.IsActive = true;
                credential.FailedSignInCount = 0;
                credential.LockedUntil = null;
                credential.UpdatedOn = timestamp;
            }

            var holdsAllAccess = !isNewUser && await _grantRepository.GetUserGrants()
                .AnyAsync(g => g.UserId == user.UserId && g.Permission == (int)PermissionEnum.TXM_Access, cancellationToken);

            var activity = isNewUser ? ActivityEnum.CreatedUser : ActivityEnum.ResetUserPassword;
            var detail = isNewUser
                ? ActivityLog.BuildDetail(activity, username, "the first administrator, from the API's start-up settings")
                : ActivityLog.BuildDetail(activity, username);

            var activityLog = ActivityLog.Create(
                Guid.NewGuid(),
                1,
                activity,
                user.UserId,
                timestamp,
                ClientAddress,
                ClientAgent,
                detail,
                ActivityLogReferenceTypeEnum.User,
                user.UserId.ToString(),
                toSignature: AccessSignature([PermissionEnum.TXM_Access]));

            await PersistAsync(() =>
            {
                if (isNewUser)
                    _userRepository.AddUser(user);
                if (isNewCredential)
                    _credentialRepository.AddUserCredential(credential);
                else
                    _credentialRepository.UpdateUserCredential(credential);
                if (!holdsAllAccess)
                    _grantRepository.AddUserGrant(NewGrant(user.UserId, PermissionEnum.TXM_Access, timestamp, grantedBy: null));
                _activityLogRepository.AddActivityLog(activityLog);
            });

            return AdministratorBootstrapEnum.Created;
        }

        // ── Reads ────────────────────────────────────────────────────────────────────────────

        private sealed class UserRow
        {
            public Guid UserId { get; set; }
            public string Username { get; set; } = null!;
            public string FirstName { get; set; } = null!;
            public string LastName { get; set; } = null!;
            public string Email { get; set; } = null!;
            public DateTime? LastSignInOn { get; set; }
            public bool HasCredential { get; set; }
            public bool? IsActive { get; set; }
            public bool? MustChangePassword { get; set; }
            public DateTime? LockedUntil { get; set; }
            public DateTime? PasswordChangedOn { get; set; }
            public DateTime? CreatedOn { get; set; }
        }

        private static Expression<Func<User, UserRow>> Row() => u => new UserRow
        {
            UserId = u.UserId,
            Username = u.Username,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            LastSignInOn = u.LastSignInOn,

            // An account without a password has no UserCredentials row: in SQL this is a left join,
            // and each column below comes back NULL for it. The null test is written last, since
            // ahead of them it would have the compiler read every one as a possible null dereference.
            IsActive = (bool?)u.UserCredential.IsActive,
            MustChangePassword = (bool?)u.UserCredential.MustChangePassword,
            LockedUntil = u.UserCredential.LockedUntil,
            PasswordChangedOn = (DateTime?)u.UserCredential.PasswordChangedOn,
            CreatedOn = (DateTime?)u.UserCredential.CreatedOn,
            HasCredential = u.UserCredential != null
        };

        private static Expression<Func<User, bool>> IsActive()
            => u => u.UserCredential != null && u.UserCredential.IsActive;

        private static Expression<Func<User, bool>> IsDeactivated()
            => u => u.UserCredential != null && !u.UserCredential.IsActive;

        // What someone is waiting on an administrator, or on the account itself, to put right: no
        // password, a lock, a password still to be replaced, or no access to sign in with.
        private static Expression<Func<User, bool>> NeedsAttention(DateTime now)
            => u => u.UserCredential == null
                || (u.UserCredential.IsActive
                    && (u.UserCredential.MustChangePassword || u.UserCredential.LockedUntil > now || !u.UserGrants.Any()));

        private static UserDTO ToDto(UserRow row, ILookup<Guid, PermissionEnum> grants, DateTime now)
        {
            var status = !row.HasCredential ? UserAccountStatusEnum.NoPassword
                : row.IsActive != true ? UserAccountStatusEnum.Deactivated
                : row.LockedUntil > now ? UserAccountStatusEnum.Locked
                : row.MustChangePassword == true ? UserAccountStatusEnum.PasswordChangeRequired
                : UserAccountStatusEnum.Active;

            return new UserDTO
            {
                UserId = row.UserId,
                Username = row.Username,
                FirstName = row.FirstName,
                LastName = row.LastName,
                Email = row.Email,
                Status = status,
                LockedUntil = status == UserAccountStatusEnum.Locked ? row.LockedUntil : null,
                Grants = grants[row.UserId].OrderBy(p => p).Select(p => p.ToString()).ToList(),
                LastSignInOn = row.LastSignInOn,
                PasswordChangedOn = row.PasswordChangedOn,
                CreatedOn = row.CreatedOn
            };
        }

        private async Task<ILookup<Guid, PermissionEnum>> GrantsOfAsync(List<Guid> userIds, CancellationToken cancellationToken)
        {
            var rows = await _grantRepository.GetUserGrants()
                .Where(g => userIds.Contains(g.UserId))
                .Select(g => new { g.UserId, g.Permission })
                .ToListAsync(cancellationToken);

            return rows
                .Where(g => Enum.IsDefined((PermissionEnum)g.Permission))
                .ToLookup(g => g.UserId, g => (PermissionEnum)g.Permission);
        }

        private async Task<List<PermissionEnum>> HeldAsync(Guid userId, CancellationToken cancellationToken)
            => (await _grantRepository.GetUserGrants()
                    .Where(g => g.UserId == userId)
                    .Select(g => g.Permission)
                    .ToListAsync(cancellationToken))
                .Select(p => (PermissionEnum)p)
                .ToList();

        private static bool ManagesUsers(IEnumerable<PermissionEnum> permissions)
            => permissions.Any(p => p is PermissionEnum.TXM_Access or PermissionEnum.TXM_ManageUsers);

        /// <summary>An active account other than <paramref name="exceptUserId"/> that can open the Users screen.</summary>
        private Task<bool> AnotherAdministratorExistsAsync(Guid exceptUserId, CancellationToken cancellationToken)
            => _grantRepository.GetUserGrants().AnyAsync(g =>
                g.UserId != exceptUserId
                && (g.Permission == (int)PermissionEnum.TXM_Access || g.Permission == (int)PermissionEnum.TXM_ManageUsers)
                && g.User.UserCredential != null
                && g.User.UserCredential.IsActive,
                cancellationToken);

        private Task<List<UserSession>> OpenSessionsAsync(Guid userId, DateTime now, CancellationToken cancellationToken)
            => _sessionRepository.GetUserSessions()
                .Where(s => s.UserId == userId && s.RevokedOn == null && s.ExpiresOn > now)
                .ToListAsync(cancellationToken);

        // ── Writes ───────────────────────────────────────────────────────────────────────────

        private UserCredential NewCredential(Guid userId, string password, bool mustChangePassword, DateTime timestamp, Guid? createdBy)
            => new()
            {
                UserId = userId,
                PasswordHash = _passwordHasher.Hash(password),
                PasswordChangedOn = timestamp,
                MustChangePassword = mustChangePassword,
                IsActive = true,
                FailedSignInCount = 0,
                CreatedOn = timestamp,
                CreatedBy = createdBy
            };

        private static UserGrant NewGrant(Guid userId, PermissionEnum permission, DateTime timestamp, Guid? grantedBy)
            => new()
            {
                UserId = userId,
                Permission = (int)permission,
                GrantedOn = timestamp,
                GrantedBy = grantedBy
            };

        private static string AccessSignature(IEnumerable<PermissionEnum> permissions)
        {
            var labels = permissions.OrderBy(p => p).Select(p => p.GetLabel()).ToList();
            return labels.Count == 0 ? "No access" : string.Join(", ", labels);
        }

        private static string Clean(string? value) => value?.Trim() ?? string.Empty;

        // ActivityLogs takes neither a null nor an over-long value, and the start-up bootstrap runs
        // outside any request.
        private string ClientAddress => Fit(_clientContext.IpAddress, 40) ?? "local";

        private string ClientAgent => Fit(_clientContext.UserAgent, 200) ?? "API start-up";

        private static string? Fit(string? value, int maxLength)
            => string.IsNullOrEmpty(value) ? null : value.Length <= maxLength ? value : value[..maxLength];

        /// <summary>
        /// Runs the supplied repository writes inside a DatabaseContext transaction. The very first
        /// statement opens the transaction (<see cref="IUnitOfWork.CommitAsync{TContext}"/> throws
        /// without one); on any failure the transaction is rolled back in a nested try/catch so a
        /// rollback error cannot mask the original exception, then the original is rethrown to the
        /// caller.
        /// </summary>
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
