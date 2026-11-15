# leyeco2-dashboard

The Transaction Monitor of LEYECO II (Leyte II Electric Cooperative, Inc., Tacloban City): a web console for monitoring the cooperative's transactions and reconciling them against their settlements. One solution, `Leyeco2Dashboard.slnx`, four projects at the repository root.

It is built on the LEYECO II HRIS (`C:\Users\EXTRASERVER\source\repos\leyeco2-hris`): the same stack, layering, credential manager and design system, under the cooperative's own seal. Where a rule here is the HRIS's unchanged, this file names it and points there rather than repeating it.

**Built so far:** sign-in (with the set-a-new-password step), the Users screen (the credential manager), the overall Dashboard (the collections ledger by channel and by town), the Online Payments dashboard (the online payment facility's payments, with reconciliation drawn below them in pencil, as a plan) and the Transaction Inspector (each bill paid online followed into the facility's posting queue and the collections ledger, with a verdict on each, exported as PDF or Excel). Reconciliation's actions and the transaction registers are the next screens.

| Project | HRIS counterpart | Role |
| --- | --- | --- |
| `Leyeco2Dashboard.App` | `Leyeco2HRIS.App` | The REST API and composition root: sign-in and the accounts (`Authentication/`, `api/auth`, `api/users`), the dashboards (`api/dashboards`), the inspector (`api/transaction-inspector`: `bills`, `bills/pdf`, `bills/xlsx`), the access catalog (`Authorization/MonitorPolicies.cs`). In Debug builds it also serves the console. |
| `Leyeco2Dashboard.Client` | `Leyeco2HRIS.Client` | The console, Blazor WebAssembly. Its `CLAUDE.md` says what differs from the HRIS's design reference. |
| `Leyeco2Dashboard.Data` | `Leyeco2HRIS.Data` | EF Core entities and contexts (this system's, the legacy ledger's, the online payment facility's), repositories, unit of work, the SQL scripts. Database-first. |
| `Leyeco2Dashboard.Services` | `Leyeco2HRIS.Services` | `SignInService`, `UserService`, `PasswordHasher`, `UserValidator`, the dashboards' services and the inspector's (`TransactionInspectionService`, its rules in `Inspection/InspectionRules.cs`), their DTOs, the printouts (`Pdf/`, `Excel/`, `Reports/`). |

## Build, run, test

Targets .NET 10 (SDK 10.0.402 on this machine).

    dotnet build Leyeco2Dashboard.slnx
    dotnet run --project Leyeco2Dashboard.App      # the API and, in Debug, the console: https://localhost:7267 (http: 5159)

- Ports are chosen clear of the HRIS's (API 7167/5059, console 7089/5190), so both run side by side. The console's own dev server, if wanted, is `https://localhost:7289` (http 5290).
- `dotnet-ef` is a local tool of `Leyeco2Dashboard.App` (`dotnet tool restore` there).
- There is no test project yet. The sign-in flow can be exercised with `curl` and a cookie jar, as the HRIS's can (`Leyeco2Dashboard.App/Authentication/CLAUDE.md`).

## Configuration

- **The API** reads `Leyeco2Dashboard.App/.env` (git-ignored; `.env.example` documents every key): the database (`ConnectionStrings__DatabaseConnection`), the dashboards' two sources (`ConnectionStrings__LegacyConnection`, `ConnectionStrings__OnlinePaymentsConnection`; optional, a dashboard without its source says so, and the inspector takes both), the session token's names and signing key (`Jwt__Issuers`, `Jwt__Audience`, `Jwt__Key__l2auth`), the first administrator (`Bootstrap__AdminUsername`, `Bootstrap__AdminPassword`, first start only), Seq (optional).
- **The console** finds the API through `Api:BaseUrl` in `Leyeco2Dashboard.Client/wwwroot/appsettings*.json`; empty means the API served it.
- **Addresses ending in `leyeco2.example`** (`monitor.leyeco2.example`, the CORS origin in `appsettings.json`) are placeholders for hosts not named yet.

## The database

`Leyeco2Dashboard`, SQL Server, five tables: `Users`, `UserCredentials`, `UserGrants`, `UserSessions` (the credential manager) and `ActivityLogs` (the audit trail). `Leyeco2Dashboard.Data/Scripts/create-schema.sql` builds them on an empty database; its header has the command that regenerates it from the model.

- The entities are EF Core Power Tools' scaffold: `Leyeco2Dashboard.Data/Entities/Database/efpt.config.json` (the HRIS's settings) lists the tables, and a new table joins that list and is re-scaffolded. Re-scaffolding overwrites the generated files; hand-written members go in partials (`ActivityLog.Factory.cs`).
- The tables are the HRIS's, less the HRIS's own: `Users` has no `EmployeeId`, and its last sign-in column is `LastSignInOn` (the HRIS keeps the upstream name `LastSyncOn`).
- `ActivityEnum` starts afresh: accounts take 100–199; transactions and reconciliation take the next ranges. `ActivityLogReferenceTypeEnum.User` is 1.
- A database is its own: this one shares no rows with the HRIS's, and an HRIS account cannot sign in here.

## The dashboards' sources

Two databases this system reads and never writes, each a scaffold of its own beside `Entities/Database` (`Entities/Legacy`, `Entities/Leyeco2OTF`, each with its `efpt.config.json`; `DatabaseContext.Reporting.cs` beside each is hand-written). They stay out of the unit of work: their repositories (`Repositories/Legacy`, `Repositories/Leyeco2OTF`) open a context per query from an `IDbContextFactory` and write their queries as T-SQL, read into the row classes of `Data/Reports`.

- **The collections ledger** (`Collection_Transactions_History`, the legacy cashiering system's): one row per bill paid and nothing of which bills were paid together, so the overall dashboard counts bills and accounts, never payments. It is the source of truth for what was collected. A bill's channel is read off the posting account's `username` in one place, `CollectionChannels.SqlCase` (`Data/Enums/CollectionChannelEnum.cs`): `sba…` is SBA, `ecpay` ECPay, `bayadctr` or `bayadcntr` Bayad Center, `smpayment` or `smpayments` SM Payment, `bizlink` or `linkbiz` Bizlink, any other a teller. (A partner may post under either of its names: the local copy, `LEYECOII`, uses `bayadcntr`, `smpayment` and `linkbiz`.) A bill's town is read off its account number in one place too, `CollectionTowns.SqlCase` (`Data/Enums/CollectionTownEnum.cs`): the number's first two characters, `01` Tacloban, `02` Palo, `03` Babatngon, any other code Others. An account is in one town, so the towns' accounts add up to the period's; the slices carry the town, and the totals are read by town. `date_paid` is a date; the time of day is in `timestamp`.
- **The online payment facility** (Leyeco2OTF's `Transactions`, kept by the `leyeco2-otf` solution's migrations): one row per bill, the bills of one payment sharing a `TransactionId`, so its dashboard counts both. Statuses are the facility's `PaymentStatusEnum` words (SUCCESSFUL, PENDING, FAILED, CANCELLED, DROPPED); an amount is `Amount + Surcharge`, each rounded to the centavo, as the facility's own summary adds them; providers are named from its `PaymentProviders`.
- **The Transaction Inspector** follows each facility bill (a `Transactions` row) through both. Its rules are in one place, `Leyeco2Dashboard.Services/Inspection/InspectionRules.cs`:
  - *The queue*: `PostedPaymentsQueue` rows of the bill's payment and bill number (the latest when there are several). Its `Status` codes are the poster's, per the cooperative (`Data/Enums/PostedPaymentQueueStatusEnum.cs`): 0 queued, 1 posting, 2 posted, -1 failed. The facility writes 0 when a provider confirms a payment, with the payment's `TransactionId` as the receipt (`ORNo`).
  - *The ledger*: its row for the bill's key, `account_no` and `ServicePeriodEnd` (its clustered key, one row per bill, as leyeco2-otf's own reconciliation tool matches them). The row is this payment's own posting when its `or_no` is the `TransactionId` (or the queue's `ORNo`); any other receipt is the bill paid through another channel, named by `CollectionChannels`. The posting account doesn't tell them apart: ECPay's outlet feed posts as `ecpay` too.
  - *Amounts*: a total is the amount plus the surcharge, each rounded to the centavo, in every book; the ledger keeps its surcharge apart from `amount_paid`, as the facility does from `Amount`. Totals within half a centavo agree.
  - *Verdicts*: paid (SUCCESSFUL) bills are tallied, amount differs, paid elsewhere, waiting to post (queued or posting, nothing in the ledger) or not posted (never queued, the poster failed, or the queue says posted and the ledger has nothing). Unpaid ones are credited, not paid (the ledger holds this payment's posting, or the queue still means to post it) or not paid. The views group them: Needs attention is amount differs, paid elsewhere, not posted and credited.
  - *Names*: `Lib_Consumers` (active consumers) and `Disconnected`, read by key. Not the facility's `Consumers2`: it is a view that unions both with a DISTINCT and a subquery per row.
  - *Reading*: a period (at most 31 days, `TransactionInspectionService.MaxDays`) is read once and kept by `ReportCache` as the dashboards' are. That is the facility's bills and queue rows, then the ledger's rows for those bills' keys, sent as XML (compatibility level 90 has no JSON or STRING_SPLIT), shredded into a keyed temporary table and loop-joined into the ledger's clustered key: a seek a bill, about 6.6 pages each (a thousand bills read 6,620 pages in 32 ms on the local copy). Every view, filter, page and printout is then cut from that read in memory. The names are a nicety: a failed read of them shows accounts by number.
- **Reading them gently.** The ledger's server is the tellers' first. Every query reads one range of its date column, compared bare against a parameter of the column's own type (`datetime` for the ledger, `datetime2` for the facility), and only the columns its covering index carries, `WITH (NOLOCK)` so a dashboard never blocks a posting, with `OPTION (RECOMPILE)`. `ReportCache` keeps a period's figures a minute (a past period, fifteen) and lets every viewer of one period share one read; a period is at most 92 days (`DashboardPeriod`).
- **The indexes** are in `Leyeco2Dashboard.Data/Scripts`: `legacy-collections-performance.sql` (`IX_CTH_Dashboard_DatePaid`; without it a few weeks of the ledger are read by scanning all of it: on the local copy three weeks went from 247,236 page reads and 4.1 s to 231 reads and 67 ms) and `online-payments-performance.sql` (the dashboard's `IX_Transactions_Dashboard_CreatedOn`, and the inspector's `IX_PostedPaymentsQueue_Inspector_TransactionId`, without which every inspection scans the whole queue). The inspector needs nothing new on the ledger: it seeks the clustered key, and `Lib_Consumers` and `Disconnected` are indexed on the account. Each script says where to run it, what it costs and how to check it.

## Accounts and sign-in

The credential manager is the HRIS's, unchanged in its rules: read the HRIS's `Leyeco2HRIS.App/CLAUDE.md` (*Sign-in and the credential manager*) and `Leyeco2HRIS.Services/CLAUDE.md` (*Accounts and sign-in*) before changing it. In short: salted PBKDF2 hashes only; five wrong passwords lock an account for fifteen minutes; accounts are deactivated, never deleted; an administrator-set password is replaced at the first sign-in (`api/auth/authenticate` answers 428); the first administrator comes from the two `Bootstrap` settings while no active account can manage users.

What is this system's own:

- **The grants** are `PermissionEnum` 1200–1299 (`AccessedSystemEnum.TransactionMonitor = 3`), clear of the HRIS's 1100–1199 so one credential manager could later hold both. `TXM_Access` is All access; `TXM_ViewDashboard`, `TXM_ViewOnlinePayments`, `TXM_InspectTransactions` and `TXM_ManageUsers` open one screen each. A grant added later reaches no account but those with All access until someone gives it on the Users screen.
- **The catalog** is `Authorization/MonitorPolicies.cs`, linked into the console as the HRIS links `HrisPolicies.cs`: grants, labels, descriptions and the groups the Users screen's access sheet draws (Monitoring, Administration), plus `HomeFor`, where an account lands (the Dashboard, else Online Payments, else the Transaction Inspector, else Users). Startup refuses a catalog that disagrees with the enum (`MonitorAuthorization.Validate`).
- **A new screen:** its member in `PermissionEnum` (and `GetLabel`); its constant, `Label`, `Describe` and group in `MonitorPolicies`; the policy on its endpoints and its page; its sidebar entry in `Layout/NavMenu.razor`, in the groups' order.
- **The cookies** are `l2auth_txm_atk` / `l2auth_txm_rtk`, so they never collide with the HRIS's `l2auth_hris_*` on one host. Changing the segment signs everyone out.

## Conventions

The HRIS's, unchanged: its root `CLAUDE.md` (*Conventions*), its `Leyeco2HRIS.App/CLAUDE.md` (controller shape, `ApiResponse`, `logId`, primitives across the boundary, explicit registration in `Program.cs`) and its `Leyeco2HRIS.Services/CLAUDE.md` (Rule 1, an audit row per write, writes in a transaction, a validator and exception per write service). The unit of work here holds one context but keeps the HRIS's generic API (`BeginTransactionAsync<DatabaseContext>()`), so a second database joins without touching a service.

## Printouts

The HRIS's printouts, carried over (its `Leyeco2HRIS.Services/CLAUDE.md`, *Printouts*): PDFs with QuestPDF 2026.7.0 and Excel workbooks with ClosedXML 0.105.1, made in `Leyeco2Dashboard.Services` (`Pdf/`, `Excel/`) from one model each (`Reports/InspectionReport.cs`), so the two say the same; the API streams them as downloads, `no-store`, and the console saves them through `wwwroot/js/files.js` (`monitorFiles.saveAs`).

- **QuestPDF runs under its Community licence** (`PrintoutLayout`), as in the HRIS. That licence is free only for an organization under USD 1M in annual gross revenue; the cooperative's licence choice covers both systems.
- **Type and seal.** PDFs set IBM Plex Sans Condensed, embedded (`Pdf/Fonts`, OFL), in ink and rules that survive a grayscale printer, every status in words. Currency is spelled `PHP` on paper, keeping to the font's own glyphs. The letterhead's seal is the console's `wwwroot/icon-192.png`, embedded by link (`Reports/Letterhead.cs`), so a new cut of the seal replaces it here too. Workbooks use Calibri, amounts as numbers, times as dates; a register is an Excel table with filters and a totals row.
- **The inspection's printouts** carry the passage's books and the verdicts, then every bill of the view (not just a page), A4 landscape. A PDF lists at most 10,000 bills (`MaxPdfBills`); a longer view goes to Excel.

## Brand and UI

Navy acts, gold marks the place, red warns; IBM Plex Sans with its Condensed cut; the register's density. The design system is the HRIS's, carried over whole: tokens and atoms in `Leyeco2Dashboard.Client/wwwroot/css/app.css` (a copy of the HRIS's, less its screens' own sections), components in `Components/Shared`. The HRIS's `Leyeco2HRIS.Client/CLAUDE.md` is the reference; `Leyeco2Dashboard.Client/CLAUDE.md` records what this console adds.

- The seal is the cooperative's own (the gear and bulb, "Est. 1975"), not the HRIS's "HR & PAYROLL" variant: `Leyeco2Dashboard.Client/wwwroot/leyeco2-logo.png` (512px, every place the console draws it, and the tab icon), `icon-192.png`, `favicon.png` (32px). Each is a square on a transparent ground, centred on the gear itself rather than on its faint drop shadow; a new cut replaces all three.
- The build credit reads "Developed by: Engr. Brian Jan Zamoras & Engr. Michael Jay Zamoras", in the status line (`Layout/AppFooter.razor`) and under the sign-in form (`Login.razor`). `Leyeco2Dashboard.Client/CLAUDE.md` says how it folds.
