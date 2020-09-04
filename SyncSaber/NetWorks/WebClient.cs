﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SyncSaber.NetWorks
{
    public static class WebClient
    {
        private static readonly HttpClient client;
        private static readonly int RETRY_COUNT = 5;

        static WebClient()
        {
            client = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
            client.DefaultRequestHeaders.UserAgent.TryParseAdd($"SyncSaber/{Assembly.GetExecutingAssembly().GetName().Version}");
        }

        internal static async Task<WebResponse> GetAsync(string url, CancellationToken token)
        {
            try {
                return await SendAsync(HttpMethod.Get, url, token);
            }
            catch (Exception e) {
                Logger.Error($"{e}");
                throw;
            }
        }

        internal static async Task<byte[]> DownloadImage(string url, CancellationToken token)
        {
            try {
                var response = await SendAsync(HttpMethod.Get, url, token);
                if (response.IsSuccessStatusCode) {
                    return response.ContentToBytes();
                }
                return null;
            }
            catch (Exception e) {
                Logger.Error($"{e}");
                throw;
            }
        }

        internal static async Task<byte[]> DownloadSong(string url, CancellationToken token, IProgress<double> progress = null)
        {
            // check if beatsaver url needs to be pre-pended
            if (!url.StartsWith(@"https://beatsaver.com/")) {
                url = $"https://beatsaver.com/{url}";
            }
            try {
                var response = await SendAsync(HttpMethod.Get, url, token, progress: progress);

                if (response.IsSuccessStatusCode) {
                    return response.ContentToBytes();
                }
                return null;
            }
            catch (Exception e) {
                Logger.Error($"{e}");
                throw;
            }
        }

        internal static async Task<WebResponse> SendAsync(HttpMethod methodType, string url, CancellationToken token, IProgress<double> progress = null)
        {
            Logger.Info($"{methodType}: {url}");

            // send request
            try {
                HttpResponseMessage resp = null;
                var retryCount = 0;
                do {
                    try {
                        // create new request messsage
                        var req = new HttpRequestMessage(methodType, url);
                        if (retryCount != 0) {
                            await Task.Delay(1000);
                        }
                        retryCount++;
                        resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                        Logger.Info($"resp code : {resp.StatusCode}");
                    }
                    catch (Exception e) {
                        Logger.Error($"Error : {e}");
                        Logger.Error($"{resp?.StatusCode}");
                    }
                } while (resp?.StatusCode != HttpStatusCode.NotFound && resp?.IsSuccessStatusCode != true && retryCount <= RETRY_COUNT);


                if (token.IsCancellationRequested) throw new TaskCanceledException();

                using (var memoryStream = new MemoryStream())
                using (var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false)) {
                    var buffer = new byte[8192];
                    var bytesRead = 0; ;

                    long? contentLength = resp?.Content.Headers.ContentLength;
                    var totalRead = 0;

                    // send report
                    progress?.Report(0);

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0) {
                        if (token.IsCancellationRequested) throw new TaskCanceledException();

                        if (contentLength != null) {
                            progress?.Report((double)totalRead / (double)contentLength);
                        }

                        await memoryStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                        totalRead += bytesRead;
                    }

                    progress?.Report(1);
                    byte[] bytes = memoryStream.ToArray();

                    return new WebResponse(resp, bytes);
                }
            }
            catch (Exception e) {
                Logger.Error($"{e}");
                throw;
            }
        }
    }
}
