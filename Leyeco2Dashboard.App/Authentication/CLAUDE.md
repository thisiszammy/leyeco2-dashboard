# Leyeco2Dashboard.Authentication

The session layer of staff sign-in, compiled into `Leyeco2Dashboard.App` as this folder (namespace `Leyeco2Dashboard.Authentication`). It mints an access token for each session the Transaction Monitor's own credential manager opens, keeps the session in the API's HttpOnly cookies, renews it silently before it expires, and authorizes requests from the token's `permission` claims.

The credential manager is in this repository: the accounts, their passwords, grants and sessions are tables of the Transaction Monitor database (the root `CLAUDE.md`, *Accounts and sign-in*), `SignInService` and `UserService` (`Leyeco2Dashboard.Services`) hold their rules, and the console's Users screen is where an administrator keeps them. This folder never reads a table itself: `AuthGatewayService` is its one way to those services.

It is the LEYECO II HRIS's session layer (`leyeco2-hris`, `Leyeco2HRIS.App/Authentication`), copied with its namespace and system changed; that in turn began as the shared auth library of the CEBECO I monorepo (`Cebeco1ERP.Authentication`), where the same seam (`IAuthGatewayService`) was an HTTP client of an outside credential manager. The cookies, the silent refresh and the policies are as they were there; what answers behind the seam is now local.

Read first:
- The root `CLAUDE.md`, *Accounts and sign-in*: the host's half (`AuthController`, `UsersApiController`, `MonitorPolicies`).
- The HRIS's `Leyeco2HRIS.Services/CLAUDE.md`, *Accounts and sign-in*: the rules a sign-in, a session and an account keep, which `SignInService` and `UserService` here keep unchanged.

## Layout

| Path | Holds |
| --- | --- |
| (no project file) | Compiled with `Leyeco2Dashboard.App`, which references `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.6 for it. `PermissionEnum` and `AccessedSystemEnum` live in `Leyeco2Dashboard.Data/Enums` (the services read them too) and reach this project through a global `using`. |
| `Extensions/AuthenticationBuilderExtensions.cs` | `AddLeyeco2Authentication(AccessedSystemEnum)`: checks the configuration, registers the JwtBearer scheme `Cookies` and its events, the policies, `AccessTokenIssuer`, `IAuthGatewayService` and `IRefreshCoalescer`. |
| `AuthConstants.cs` | The claim type names, the host's system and its grants (`AccessedSystem`, `Permissions`, `AllAccess`), `ToClaimValue` and `TryParsePermission`. |
| `AuthenticationCookies.cs` | The cookie names `l2auth_{segment}_{rtk,atk,rm}` (`txm` for the Transaction Monitor, so its cookies never meet the HRIS's `l2auth_hris_*` on a shared host), and `Stamp` / `Clear` / `IsPersistent`. |
| `AuthenticationSchemes.cs`, `AuthorizationPolicies.cs` | The scheme name `Cookies`. The policies: `SystemAccess` (also the default), `Authenticated`, one per grant of the block; `AnyGrantOf(...)` builds a host's own policy met by any of several grants. |
| `AuthChallengeResolver.cs` | The answer of a failed silent refresh: a status for `/api`, pass-through for everything else. |
| `Middleware/AccessTokenRefreshMiddleware.cs` | Silent refresh, run before `UseAuthentication`. |
| `Services/AccessTokenIssuer.cs` | Mints the access token of a session: HS256 under `Jwt:Key:l2auth`, the account's id, names and grants as claims. |
| `Services/AuthGatewayService.cs`, `IAuthGatewayService.cs` | The seam to the credential manager: sign in, refresh, sign out, change password, each answering an `AuthGatewayResult` (the account, its token, the session token and its expiry). |
| `Services/AuthGatewayException.cs` | A refresh that failed; `Unauthorized` is the one verdict on a session. |
| `Services/RefreshCoalescer.cs`, `IRefreshCoalescer.cs` | One refresh for a burst of requests under the same lapsed token. |

## How it connects

- **The host.** `Program.cs` calls `builder.AddLeyeco2Authentication(AccessedSystemEnum.TransactionMonitor)` and `app.UseMiddleware<AccessTokenRefreshMiddleware>()`, and `AuthController` (`api/auth/authenticate|change-password|logout|session`) is the HTTP contract of the console. The Blazor client cannot reference this folder: it keeps its own `AuthorizationPolicies` with the same policy names (in `Infrastructure/Auth`, registering the ones `Authorization/MonitorPolicies.cs` names, a file both compile). The host also registers the policies several of its screens share, with `AnyGrantOf`, and answers a refused `/api` request with an `ApiResponse` naming the permission it takes (`MissingGrantResultHandler`).
- **The credential manager, in process.** `AuthGatewayService` is a singleton that opens a DI scope per call and resolves `ISignInService` in it, so each call has a `DatabaseContext` of its own: the coalescer lets a refresh outlive the request that started it, and a failed write must leave nothing staged on the context a request's own save uses.

  | Method | Calls | Answers |
  | --- | --- | --- |
  | `AuthenticateAsync` | `SignInService.SignInAsync` | The session; a refusal is a `SignInException` (wrong credentials, locked, deactivated, no access, password change required), which `AuthController` maps to a status. |
  | `ChangePasswordAsync` | `SignInService.ChangePasswordAsync` | The session re-opened under the new password; `SignInException` or `UserValidationException` otherwise. |
  | `RefreshAsync` (only through `IRefreshCoalescer`) | `SignInService.RefreshAsync` | The same session, pushed back, with a newly minted token; `AuthGatewayException(Unauthorized)` when the session is over. |
  | `LogoutAsync` | `SignInService.SignOutAsync` | Nothing; failures are logged, never thrown. |

- **Two tokens, two lifetimes.** The access token (`atk`) is a JWT this host mints and validates, short-lived (`Jwt:AccessExpiryMinutes`, 5 by default), and what authorizes a request: no table is read. The session token (`rtk`) is 32 random bytes whose SHA-256 is all `UserSessions` keeps; it is not rotated, and presenting it mints the next access token after reading the account and its grants again. That read is how a deactivation, a lock-out and a changed grant reach a signed-in account: at the next refresh, so within the access token's lifetime.

## Conventions

- **Static configuration, set once.** `AddLeyeco2Authentication` calls `AuthConstants.Configure` and `AuthenticationCookies.Configure`; reading `AccessedSystem`, `Permissions` or a cookie name before then throws an `InvalidOperationException` naming the missing call. `Configure` refuses a system with no `PermissionEnum` range or no cookie segment.
- **Configuration keys.** The first three are required; a missing one fails startup and the error names the env var.

  | Key | Use |
  | --- | --- |
  | `Jwt:Issuers` | Comma-separated. The first entry goes into every token; each is accepted on the way back, so a name can be changed without signing anyone out. |
  | `Jwt:Audience` | Comma-separated, read the same way. Access is decided by the grants, not the audience. |
  | `Jwt:Key:{kid}` | Signing keys by key id. `l2auth` signs every token and must be at least 32 characters; any other is still accepted, which serves a rotation: add the new key under `l2auth`, keep the old one under another id for one access-token lifetime, then drop it. |
  | `Jwt:AccessExpiryMinutes` | The access token's lifetime, default 5: how long a revoked grant or a deactivation can outlive its change. |
  | `Auth:CookieDomain` | The `Domain` of every cookie; empty means host-only |
  | `Auth:PreemptiveRefreshSeconds` | The silent-refresh window, default 60; 0 or less renews only a lapsed token |

  The sessions' own settings (`Auth:SessionHours`, `Auth:MaxFailedSignIns`, `Auth:LockoutMinutes`) are `CredentialOptions`, read in `Program.cs` for the services.
- **Cookies.** Every Append and Delete goes through `AuthenticationCookies`: HttpOnly, SameSite Strict, Path `/`, the configured `Domain`, `Secure` following `Request.IsHttps`, and no expiry unless the session is a kept one (the Transaction Monitor's sign-in never asks for one, so its cookies end with the browser). A Delete carries the options of the Append it undoes.
- **Grants.** A claim counts only when it is the exact member name of a grant in the host's range (`TryParsePermission`): numbers, names this build does not define and other systems' grants open nothing. A new grant in the range gets its policy with no code here. The system's all-access grant (`PermissionEnumExtensions.GetAllAccess`, read into `AuthConstants.AllAccess`; the Transaction Monitor's `TXM_Access`) meets every per-grant policy and every `AnyGrantOf` policy; `SystemAccess` stays "any grant of the range".
- **Silent refresh.** A missing or lapsed token is renewed before the request authenticates; one inside the window is renewed in passing. A session that is over clears the cookies (401 on `/api`); any other failure (the database out of reach) keeps them, lets a still-valid token carry the request, and answers 503 on `/api` only when the token has lapsed. A token this host would refuse for another reason (signature, issuer, audience) is left to JwtBearer: a refresh would be signed the same way. The middleware skips `api/auth/authenticate`, `change-password` and `logout`, which open, replace or end the session themselves.
- **DI lifetimes.** `AccessTokenIssuer`, the gateway and `IRefreshCoalescer` are singletons (the coalescer's in-flight map spans requests); the services the gateway resolves are scoped, in the scope it opens.
- **Logging.** Never a password or a token; a session token only as `RefreshCoalescer.HashTokenId` (8 hex characters). JWT validation failures log under `Leyeco2Dashboard.Authentication.JwtBearer`.

## Build and test

- It builds with `Leyeco2Dashboard.App`.
- No test project. The whole of it can be exercised on a development machine with `curl` and a cookie jar: sign in (`POST api/auth/authenticate`), then call any endpoint with the jar; a request carrying the `rtk` cookie alone shows the silent refresh.

## Gotchas

- **One system per process.** The configuration is static: a second `AddLeyeco2Authentication` in one process replaces the first host's system and cookie names.
- **The cookie names are live in browsers.** Changing a segment signs every user of that host out. So does changing `Jwt:Key:l2auth` without keeping the old key under another id.
- **A change reaches a session at its next refresh.** Deactivating an account, resetting its password or changing its password ends its sessions at once in `UserSessions`, but an access token already minted stays good until it lapses (5 minutes by default). Lower `Jwt:AccessExpiryMinutes` to shorten that; nothing revokes a token before its time.
- **`RefreshCoalescer` is per process.** Behind a load balancer two nodes may each refresh the same session at once; nothing breaks (the session token is not rotated), each just mints a token.
- **`Secure` follows `Request.IsHttps`** and no forwarded headers are read: behind a TLS-terminating proxy the cookies go out without `Secure`, and the sign-in rate limit sees every caller at the proxy's address (`Program.cs`).
- **`OnChallenge` clears the cookies** unless `AccessTokenRefreshMiddleware.RefreshedItemKey` is set: Delete would strip the Appends a refresh just made.

Comment policy: Do not add comments that restate code. Do not add change-log/history comments. Do not leave commented-out code. Only comment WHY (business rule, constraint, workaround, integration point). Keep XML docs only for public APIs of shared libraries.
