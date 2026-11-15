using System.Globalization;
using Leyeco2Dashboard.Client.Infrastructure.Api;
using Microsoft.AspNetCore.Components;

namespace Leyeco2Dashboard.Client.Components.Shared
{
    /// <summary>
    /// What the dashboards share: the period lives in the address (<c>?from=2026-06-01&amp;to=2026-06-06</c>, none
    /// for today), so a period can be bookmarked or sent and Back returns to the one before; a newer pick cancels
    /// the read it replaces; and while the period includes today the figures are read again every two minutes
    /// (the API keeps them a minute, so that is at most one read of its database per period, however many have
    /// the page open). A failed read of the period on screen keeps its figures, saying so; a failed read of a new
    /// period clears them.
    /// </summary>
    public abstract class DashboardPage<T> : ComponentBase, IDisposable where T : class
    {
        private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(2);

        private readonly CancellationTokenSource _disposed = new();
        private CancellationTokenSource? _read;
        private (DateTime From, DateTime To)? _shown;

        [Inject] protected NavigationManager Nav { get; set; } = default!;

        [SupplyParameterFromQuery(Name = "from")] public string? FromQuery { get; set; }
        [SupplyParameterFromQuery(Name = "to")] public string? ToQuery { get; set; }

        protected DateTime From { get; private set; } = DateTime.Today;
        protected DateTime To { get; private set; } = DateTime.Today;

        /// <summary>The figures on screen, for <see cref="From"/>–<see cref="To"/> once read.</summary>
        protected T? Data { get; private set; }

        protected string? LoadError { get; private set; }

        protected bool Loading { get; private set; }

        /// <summary>The page's own path ("/", "/online-payments"), which its period is added to.</summary>
        protected abstract string PagePath { get; }

        protected abstract Task<T> ReadAsync(DateTime from, DateTime to, CancellationToken ct);

        /// <summary>Whether the figures still move: the period they were read for includes today.</summary>
        protected abstract bool IsLive(T data);

        /// <summary>Called when the address names another period, before it is read: reset what follows the period.</summary>
        protected virtual void OnPeriodChanged()
        {
        }

        protected override void OnInitialized() => _ = RefreshWhileLiveAsync(_disposed.Token);

        protected override async Task OnParametersSetAsync()
        {
            // A link out of the page hands it the next address's query before the router leaves: not this page's.
            if (!IsOwnAddress())
                return;

            var (from, to) = ParsePeriod();
            if (_shown == (from, to) || (Loading && From == from && To == to))
                return;

            From = from;
            To = to;
            OnPeriodChanged();
            await LoadAsync();
        }

        protected Task ChangePeriodAsync((DateTime From, DateTime To) period)
        {
            var today = DateTime.Today;
            var address = period.From == today && period.To == today
                ? PagePath
                : $"{PagePath}?from={Iso(period.From)}&to={Iso(period.To)}";

            Nav.NavigateTo(address);
            return Task.CompletedTask;
        }

        protected async Task LoadAsync()
        {
            _read?.Cancel();
            _read?.Dispose();
            var read = _read = new CancellationTokenSource();
            var period = (From, To);

            Loading = true;
            try
            {
                var data = await ReadAsync(period.From, period.To, read.Token);
                Data = data;
                LoadError = null;
                _shown = period;
            }
            catch (OperationCanceledException) when (read.IsCancellationRequested)
            {
                return;
            }
            catch (ApiException ex)
            {
                LoadError = ex.Message;
                if (_shown != period)
                {
                    Data = null;
                    _shown = null;
                }
            }
            finally
            {
                if (ReferenceEquals(_read, read))
                    Loading = false;
            }
        }

        private async Task RefreshWhileLiveAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(RefreshEvery);
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    if (Loading || Data is null || !IsLive(Data) || !IsOwnAddress())
                        continue;

                    await InvokeAsync(async () =>
                    {
                        await LoadAsync();
                        StateHasChanged();
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private bool IsOwnAddress()
        {
            var path = "/" + Nav.ToBaseRelativePath(Nav.Uri).Split('?', '#')[0].Trim('/');
            return string.Equals(path, PagePath, StringComparison.OrdinalIgnoreCase);
        }

        // Neither date is today; one alone is that day; a reversed pair is put the right way round.
        private (DateTime From, DateTime To) ParsePeriod()
        {
            var from = Parse(FromQuery);
            var to = Parse(ToQuery);
            var start = from ?? to ?? DateTime.Today;
            var end = to ?? from ?? DateTime.Today;
            return end < start ? (end, start) : (start, end);
        }

        private static DateTime? Parse(string? value)
            => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

        private static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public virtual void Dispose()
        {
            _disposed.Cancel();
            _disposed.Dispose();
            _read?.Cancel();
            _read?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
