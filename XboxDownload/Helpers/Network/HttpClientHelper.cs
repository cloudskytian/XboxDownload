using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace XboxDownload.Helpers.Network;

public partial class HttpClientHelper
{
    private static IHttpClientFactory? HttpClientFactory => App.Services?.GetRequiredService<IHttpClientFactory>();
    
    public static async Task<string> GetStringContentAsync(string url, string method = "GET", string? postData = null, string? contentType = null, Dictionary<string, string>? headers = null, int timeout = 30000, string? name = null, string? charset = null, CancellationToken token = default)
    {
        using var response = await SendRequestAsync(url, method, postData, contentType, headers, timeout, name, token);
        if (response is not { IsSuccessStatusCode: true }) return string.Empty;
        if (charset is null) return await response.Content.ReadAsStringAsync(token);
        var responseBytes = await response.Content.ReadAsByteArrayAsync(token);
        return Encoding.GetEncoding(charset).GetString(responseBytes);
    }

    public static async Task<HttpResponseMessage?> SendRequestAsync(string url, string method = "GET", string? postData = null, string? contentType = null, Dictionary<string, string>? headers = null, int timeout = 30000, string? name = null, CancellationToken token = default)
    {
        var client = HttpClientFactory?.CreateClient(name ?? "Default");
        if (client == null) return null;
        client.Timeout = TimeSpan.FromMilliseconds(timeout);
        if (headers != null)
        {
            foreach (var (key, value) in headers)
                client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
        }
        HttpRequestMessage httpRequestMessage = new()
        {
            Method = new HttpMethod(method),
            RequestUri = new Uri(url)
        };
        if (httpRequestMessage.Method == HttpMethod.Post || httpRequestMessage.Method == HttpMethod.Put)
            httpRequestMessage.Content = new StringContent(postData ?? string.Empty, Encoding.UTF8, contentType ?? "application/x-www-form-urlencoded");
        HttpResponseMessage? response;
        try
        {
            response = await client.SendAsync(httpRequestMessage, token);
        }
        catch (HttpRequestException ex)
        {
            response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                ReasonPhrase = ex.Message
            };
        }
        catch (Exception ex)
        {
            response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = ex.Message
            };
        }
        return response;
    }
    
    public static async Task<string?> GetFastestProxy(string[] proxies, string path, Dictionary<string, string> headers, int timeout)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));

        var tasks = proxies.Select(async proxy =>
        {
            var url = proxy + (string.IsNullOrEmpty(proxy) ? path : path.Replace("https://", ""));
            using var response = await SendRequestAsync(url, headers: headers, timeout: timeout, name: "NoCache", token: cts.Token);
            if (response is not { IsSuccessStatusCode: true }) return null;
            using var ms = new MemoryStream();
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                await stream.CopyToAsync(ms, cts.Token);
                return url;
            }
            catch (TaskCanceledException) { }
            catch (Exception)
            {
                // ignored
            }
            return null;
        }).ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);
            var fastestUrl = await completedTask;
            if (fastestUrl == null) continue;
            await cts.CancelAsync();
            return fastestUrl;
        }
        return null;
    }
    
    public static async Task<IPAddress?> GetFastestIp(IPAddress[] ips, int port, int timeout)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));

        var tasks = ips.Select(async ip =>
        {
            using var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = timeout;
            socket.ReceiveTimeout = timeout;
            try
            {
                var connectTask = Task.Factory.FromAsync(socket.BeginConnect, socket.EndConnect, new IPEndPoint(ip, port), null);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(timeout, cts.Token));
                return (completedTask == connectTask && socket.Connected) ? ip : null;
            }
            catch
            {
                return null;
            }
        }).ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);
            var fastestIp = await completedTask;
            if (fastestIp == null) continue;
            await cts.CancelAsync();
            return fastestIp;
        }
        return null;
    }
    
    public static readonly HttpClient SharedHttpClient;

    static HttpClientHelper()
    {
        var handler = new HttpClientHandler()
        {
            CookieContainer = new CookieContainer()
        };
        
        SharedHttpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        SharedHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(nameof(XboxDownload));
    }

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error opening URL: " + ex.Message);
        }
    }
    
    public static bool SniProxy(IPAddress[] ips, string? sni, byte[] send1, byte[] send2, SslStream client, out string? errMessage)
    {
        var isOk = true;
        errMessage = null;
        using Socket mySocket = new(ips[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        mySocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, true);
        mySocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, true);
        mySocket.SendTimeout = 6000;
        mySocket.ReceiveTimeout = 6000;
        try
        {
            mySocket.Connect(ips, 443);
        }
        catch (Exception ex)
        {
            isOk = false;
            errMessage = ex.Message;
        }
        if (mySocket.Connected)
        {
            using SslStream ssl = new(new NetworkStream(mySocket), false, delegate { return true; }, null);
            ssl.WriteTimeout = 30000;
            ssl.ReadTimeout = 30000;
            try
            {
                ssl.AuthenticateAsClient(string.IsNullOrEmpty(sni) ? ips[0].ToString() : sni);
                if (ssl.IsAuthenticated)
                {
                    ssl.Write(send1);
                    ssl.Write(send2);
                    ssl.Flush();
                    long count = 0, contentLength = -1;
                    int len;
                    string headers = string.Empty, transferEncoding = string.Empty;
                    var list = new List<byte>();
                    var bReceive = new byte[4096];
                    while ((len = ssl.Read(bReceive, 0, bReceive.Length)) > 0)
                    {
                        count += len;
                        var dest = new byte[len];
                        if (len == bReceive.Length)
                        {
                            dest = bReceive;
                            if (string.IsNullOrEmpty(headers)) list.AddRange(bReceive);
                        }
                        else
                        {
                            Buffer.BlockCopy(bReceive, 0, dest, 0, len);
                            if (string.IsNullOrEmpty(headers)) list.AddRange(dest);
                        }
                        client.Write(dest);
                        if (string.IsNullOrEmpty(headers))
                        {
                            var bytes = list.ToArray();
                            for (var i = 1; i <= bytes.Length - 4; i++)
                            {
                                if (BitConverter.ToString(bytes, i, 4) != "0D-0A-0D-0A") continue;
                                
                                headers = Encoding.ASCII.GetString(bytes, 0, i + 4);
                                count = bytes.Length - i - 4;
                                list.Clear();
                                var result = StatusCodeHeaderRegex().Match(headers);
                                if (result.Success && int.TryParse(result.Groups["StatusCode"].Value, out var statusCode) && statusCode >= 400)
                                {
                                    isOk = false;
                                }
                                result = ContentLengthHeaderRegex().Match(headers);
                                if (result.Success)
                                {
                                    contentLength = Convert.ToInt32(result.Groups["ContentLength"].Value);
                                }
                                result = TransferEncodingHeaderRegex().Match(headers);
                                if (result.Success)
                                {
                                    transferEncoding = result.Groups["TransferEncoding"].Value.Trim();
                                }
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(headers))
                        {
                            if (transferEncoding == "chunked")
                            {
                                if (dest.Length >= 5 && BitConverter.ToString(dest, dest.Length - 5) == "30-0D-0A-0D-0A")
                                {
                                    break;
                                }
                            }
                            else if (contentLength >= 0)
                            {
                                if (count == contentLength) break;
                            }
                            else break;
                        }
                    }
                    client.Flush();
                }
            }
            catch (Exception ex)
            {
                isOk = false;
                errMessage = ex.Message;
            }
            finally
            {
                ssl.Close();
            }
        }

        if (!mySocket.Connected) return isOk;
        try
        {
            mySocket.Shutdown(SocketShutdown.Both);
        }
        finally
        {
            mySocket.Close();
        }

        return isOk;
    }
    
    [GeneratedRegex(@"^HTTP/\d+(\.\d*)? (?<StatusCode>\d+)")]
    private static partial Regex StatusCodeHeaderRegex();
    
    [GeneratedRegex(@"Content-Length:\s*(?<ContentLength>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ContentLengthHeaderRegex();
    
    [GeneratedRegex(@"Transfer-Encoding:\s*(?<TransferEncoding>.+)", RegexOptions.IgnoreCase)]
    private static partial Regex TransferEncodingHeaderRegex();
}