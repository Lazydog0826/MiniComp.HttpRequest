using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting;
using MiniComp.Core.App;
using MiniComp.Core.Extension;
using Newtonsoft.Json;

namespace MiniComp.HttpRequest;

public static class HttpRequest
{
    /// <summary>
    /// 请求（需手动释放HttpResponseMessage资源）
    /// </summary>
    /// <param name="httpRequestInfo"></param>
    /// <param name="httpCompletionOption"></param>
    /// <returns></returns>
    /// <exception cref="HttpRequestException"></exception>
    public static async Task<HttpResponseMessage> RequestAsync(
        HttpRequestModel httpRequestInfo,
        HttpCompletionOption httpCompletionOption = HttpCompletionOption.ResponseContentRead
    )
    {
        using var httpclientHandler = new HttpClientHandler();
        // 压缩和解压缩编码格式
        httpclientHandler.AutomaticDecompression =
            httpRequestInfo.AutomaticDecompression ?? httpclientHandler.AutomaticDecompression;
        // Cookie
        httpclientHandler.CookieContainer =
            httpRequestInfo.CookieContainer ?? httpclientHandler.CookieContainer;
        // 如果是开发环境，忽略证书验证
        if (HostApp.HostEnvironment.IsDevelopment())
        {
            httpclientHandler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        using var client = HttpClientFactory.Create(httpclientHandler);
        if (httpRequestInfo.Timeout.HasValue)
        {
            // 官方文档说明默认时间为：100000毫秒
            client.Timeout = TimeSpan.FromSeconds(httpRequestInfo.Timeout.Value);
        }
        var httpRequestMessage = new HttpRequestMessage(
            httpRequestInfo.HttpMethod,
            httpRequestInfo.UriBuilder.ToString()
        );
        if (httpRequestInfo.Heads != null && httpRequestInfo.Heads.Count != 0)
        {
            foreach (var key in httpRequestInfo.Heads.Keys)
            {
                httpRequestMessage.Headers.Add(key, httpRequestInfo.Heads[key]);
            }
        }
        httpRequestMessage.Content = httpRequestInfo.HttpContent;
        if (
            httpRequestMessage.Content != null
            && !string.IsNullOrEmpty(httpRequestInfo.RequestContentType)
        )
        {
            // 设置请求头ContentType属性
            httpRequestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(
                httpRequestInfo.RequestContentType
            );

            // 设置编码格式
            if (httpRequestInfo.Encoding != null)
            {
                httpRequestMessage.Content.Headers.ContentType.CharSet = httpRequestInfo
                    .Encoding
                    .WebName;
            }
        }
        if (!string.IsNullOrEmpty(httpRequestInfo.ResponseContentType))
        {
            // 指定客户端能够接受的内容类型
            httpRequestMessage.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(httpRequestInfo.ResponseContentType)
            );
        }

        using var periodicTimer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(httpRequestInfo.RetryInterval)
        );
        var retryCount = 0;
        HttpResponseMessage? response = null;
        Exception? exception = null;
        HttpRequestError? httpRequestError = null;
        while (await periodicTimer.WaitForNextTickAsync())
        {
            if (retryCount > httpRequestInfo.RetryCount)
            {
                throw new HttpRequestException(
                    httpRequestError ?? HttpRequestError.Unknown,
                    "请求超出重试次数",
                    exception,
                    response?.StatusCode
                );
            }
            try
            {
                response = await client.SendAsync(httpRequestMessage, httpCompletionOption);
            }
            catch (Exception ex)
            {
                exception = ex;
                if (ex is HttpRequestException hre)
                {
                    httpRequestError = hre.HttpRequestError;
                }
            }
            finally
            {
                retryCount++;
            }
            if (response is { IsSuccessStatusCode: true })
            {
                break;
            }
        }
        return response
            ?? throw new HttpRequestException(
                httpRequestError ?? HttpRequestError.Unknown,
                "未知错误",
                exception,
                response?.StatusCode
            );
    }

    /// <summary>
    /// 请求并返回结果
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="httpRequestInfo"></param>
    /// <returns></returns>
    public static async Task<T?> RequestAsync<T>(HttpRequestModel httpRequestInfo)
    {
        using var response = await RequestAsync(httpRequestInfo);
        try
        {
            var resType = typeof(T);
            var resData = resType switch
            {
                _ when resType == typeof(string) => await response.Content.ReadAsStringAsync(),
                _ when resType == typeof(Stream) => await response.Content.ReadAsStreamAsync(),
                _ when resType == typeof(byte[]) => await response.Content.ReadAsByteArrayAsync(),
                _ => await DeserializeResponse<T>(response, httpRequestInfo.ResponseContentType),
            };
            return resData.ChangeType<T>();
        }
        catch (Exception ex)
        {
            throw new HttpRequestException(
                HttpRequestError.Unknown,
                "读取响应失败",
                ex,
                response.StatusCode
            );
        }
    }

    /// <summary>
    /// 读取字符串并序列化
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="response"></param>
    /// <param name="contentType"></param>
    /// <returns></returns>
    private static async Task<object?> DeserializeResponse<T>(
        this HttpResponseMessage response,
        string? contentType
    )
    {
        var content = await response.Content.ReadAsStringAsync();
        return (contentType?.ToLower()) switch
        {
            "application/json" => JsonConvert.DeserializeObject<T>(content),
            "text/xml" or "application/xml" => content.DeserializationXml<T>(),
            _ => content,
        };
    }
}
