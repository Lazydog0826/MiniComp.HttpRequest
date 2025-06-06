using System.Net;
using System.Text;

namespace MiniComp.HttpRequest;

public class HttpRequestModel
{
    /// <summary>
    /// 请求地址
    /// </summary>
    public UriBuilder UriBuilder { get; set; } = new();

    /// <summary>
    /// 请求方式(默认POST)
    /// </summary>
    public HttpMethod HttpMethod { get; set; } = HttpMethod.Post;

    /// <summary>
    /// 请求头
    /// </summary>
    public Dictionary<string, string>? Heads { get; set; } = [];

    /// <summary>
    /// 内容类型(默认JSON)
    /// </summary>
    public string? RequestContentType { get; set; } = null;

    /// <summary>
    /// 响应内容格式(默认JSON)
    /// </summary>
    public string? ResponseContentType { get; set; } = null;

    /// <summary>
    /// 请求内容
    /// </summary>
    public HttpContent? HttpContent { get; set; } = null;

    /// <summary>
    /// 编码
    /// </summary>
    public Encoding? Encoding { get; set; } = Encoding.Default;

    /// <summary>
    /// 超时时间(单位：毫秒) - 默认100秒
    /// </summary>
    public long? Timeout { get; set; } = 100 * 1000;

    /// <summary>
    /// 是否解析压缩
    /// </summary>
    public DecompressionMethods? AutomaticDecompression { get; set; } = null;

    /// <summary>
    /// Cookie
    /// </summary>
    public CookieContainer? CookieContainer { get; set; } = null;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔（单位毫秒） - 默认1秒
    /// </summary>
    public int RetryInterval { get; set; } = 1000;
}
