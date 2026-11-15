using System.Net.Http.Json;
using System.Text.Json;
using Leyeco2Dashboard.Client.Infrastructure.Auth;

namespace Leyeco2Dashboard.Client.Infrastructure.Api
{
    /// <summary>
    /// Thin wrapper over <see cref="HttpClient"/> that unwraps the Transaction Monitor API's response envelope and
    /// converts every failure mode (unreachable server, timeout, non-2xx, unreadable body) into an
    /// <see cref="ApiException"/> whose message is written for end users. Cancellation requested by
    /// the caller (e.g. a superseded debounced search) surfaces as
    /// <see cref="OperationCanceledException"/> so call sites can ignore it silently.
    /// </summary>
    public class ApiClient
    {
        private readonly HttpClient _http;
        private readonly SessionAuthenticationStateProvider _auth;

        public ApiClient(HttpClient http, SessionAuthenticationStateProvider auth)
        {
            _http = http;
            _auth = auth;
        }

        /// <summary>
        /// The API renews the session silently before it authorizes a request, so a 401 here means
        /// the session is over (its refresh token expired, or was revoked or signed out elsewhere):
        /// the shell goes back to the login screen, which says why. A 403 is a grant the account
        /// lacks, each screen having its own, and the API's message names it for the caller to show;
        /// the session is read again meanwhile, so a grant revoked since the shell last read it hides
        /// what it opened, and an account left with none is signed out.
        /// </summary>
        private void HandleSessionFailure(int statusCode)
        {
            if (statusCode == 401)
                _auth.EndSession();
            else if (statusCode == 403)
                _ = _auth.RevalidateAsync();
        }

        public async Task<T> GetDataAsync<T>(string uri, CancellationToken ct = default)
        {
            var envelope = await ExchangeAsync<T>(HttpMethod.Get, uri, body: null, ct);

            if (envelope.Data is null)
                throw new ApiException("The server response was empty. Please try again.");

            return envelope.Data;
        }

        public Task PostAsync(string uri, object body, CancellationToken ct = default)
            => ExchangeAsync<object>(HttpMethod.Post, uri, body, ct);

        /// <summary>POSTs <paramref name="body"/> and returns the response envelope's data (never null).</summary>
        public async Task<T> PostDataAsync<T>(string uri, object body, CancellationToken ct = default)
        {
            var envelope = await ExchangeAsync<T>(HttpMethod.Post, uri, body, ct);

            if (envelope.Data is null)
                throw new ApiException("The server response was empty. Please try again.");

            return envelope.Data;
        }

        public Task PutAsync(string uri, object body, CancellationToken ct = default)
            => ExchangeAsync<object>(HttpMethod.Put, uri, body, ct);

        /// <summary>PUTs <paramref name="body"/> and returns the response envelope's data (never null).</summary>
        public async Task<T> PutDataAsync<T>(string uri, object body, CancellationToken ct = default)
        {
            var envelope = await ExchangeAsync<T>(HttpMethod.Put, uri, body, ct);

            if (envelope.Data is null)
                throw new ApiException("The server response was empty. Please try again.");

            return envelope.Data;
        }

        public Task DeleteAsync(string uri, CancellationToken ct = default)
            => ExchangeAsync<object>(HttpMethod.Delete, uri, body: null, ct);

        /// <summary>POSTs multipart form data (file uploads) and returns the envelope's data (never null).</summary>
        public async Task<T> PostMultipartDataAsync<T>(string uri, MultipartFormDataContent content, CancellationToken ct = default)
        {
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
                response = await _http.SendAsync(request, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                throw new ApiException("The upload timed out. Please try again.", null, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException("Cannot reach the Transaction Monitor server. Make sure the API is running, then try again.", null, ex);
            }

            using (response)
            {
                var envelope = await ReadEnvelopeAsync<T>(response, ct);

                if (envelope.Data is null)
                    throw new ApiException("The server response was empty. Please try again.");

                return envelope.Data;
            }
        }

        /// <summary>
        /// GETs a raw file body (download endpoints that skip the envelope). Error responses still
        /// arrive as envelopes, so failures surface with the server's message.
        /// </summary>
        public async Task<byte[]> GetBytesAsync(string uri, CancellationToken ct = default)
        {
            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                throw new ApiException("The download timed out. Please try again.", null, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException("Cannot reach the Transaction Monitor server. Make sure the API is running, then try again.", null, ex);
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    HandleSessionFailure((int)response.StatusCode);

                    var (envelope, plainText) = await ReadBodyAsync<object>(response, ct);
                    throw new ApiException(FailureMessage(response, envelope, plainText), (int)response.StatusCode);
                }

                return await response.Content.ReadAsByteArrayAsync(ct);
            }
        }

        private async Task<ApiEnvelope<T>> ExchangeAsync<T>(HttpMethod method, string uri, object? body, CancellationToken ct)
        {
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(method, uri);
                if (body is not null)
                    request.Content = JsonContent.Create(body);

                response = await _http.SendAsync(request, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // the caller abandoned this request — not an error
            }
            catch (TaskCanceledException ex)
            {
                throw new ApiException("The request timed out. Please try again.", null, ex);
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException("Cannot reach the Transaction Monitor server. Make sure the API is running, then try again.", null, ex);
            }

            using (response)
            {
                return await ReadEnvelopeAsync<T>(response, ct);
            }
        }

        private async Task<ApiEnvelope<T>> ReadEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
            {
                // A refusal carries a message and null data, so it is read untyped, as GetBytesAsync
                // reads one: a T that cannot be null (a create answering with its new id) fails to
                // deserialize that null, which would replace the server's reason with generic copy.
                var (failure, failureText) = await ReadBodyAsync<object>(response, ct);
                HandleSessionFailure((int)response.StatusCode);
                throw new ApiException(FailureMessage(response, failure, failureText), (int)response.StatusCode);
            }

            var (envelope, _) = await ReadBodyAsync<T>(response, ct);

            if (envelope is null)
                throw new ApiException("The server returned an unreadable response. Please try again.");

            return envelope;
        }

        /// <summary>
        /// The envelope, or for a plain-text answer its text. The API's own answers are envelopes;
        /// the plain text comes from the session layer in front of it, which answers a session it
        /// cannot renew right now (the database out of reach) with a 503 and a sentence meant for
        /// the user.
        /// </summary>
        private static async Task<(ApiEnvelope<T>? Envelope, string? PlainText)> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken ct)
        {
            try
            {
                if (response.Content.Headers.ContentType?.MediaType == "text/plain")
                    return (null, (await response.Content.ReadAsStringAsync(ct)).Trim());

                return (await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(cancellationToken: ct), null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException)
            {
                // Not an envelope (proxy error page, empty body, …) — the caller falls back to generic copy.
                return (null, null);
            }
        }

        private static string FailureMessage<T>(HttpResponseMessage response, ApiEnvelope<T>? envelope, string? plainText)
        {
            if (!string.IsNullOrWhiteSpace(envelope?.Message))
                return envelope!.Message!;

            if (!string.IsNullOrWhiteSpace(plainText))
                return plainText;

            return (int)response.StatusCode == 403
                ? "Your account doesn't hold the permission this needs. Ask an administrator to give it on the Users screen."
                : $"The server returned an unexpected error ({(int)response.StatusCode}). Please try again.";
        }
    }
}
