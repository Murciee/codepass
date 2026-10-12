using System;
using System.Net;
using System.Threading;

namespace Codepass
{
    static class ConnectionProbe
    {
        internal static OperationResult Http(string url, string token, bool ntfy, CancellationToken cancellation)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = false; request.Timeout = 5000; request.ReadWriteTimeout = 5000;
            request.UserAgent = "codepass/1.0";
            if (!ntfy) request.Proxy = null; // ntfy 与正式订阅共用系统代理策略。
            if (token.Length > 0) request.Headers[ntfy ? "Authorization" : "X-Token"] = (ntfy ? "Bearer " : "") + token;
            using (cancellation.Register(request.Abort))
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode != HttpStatusCode.OK) return new OperationResult(OperationState.Failed, "服务器响应异常：HTTP " + (int)response.StatusCode);
                        return new OperationResult(OperationState.Success, ntfy ? "ntfy 连接与鉴权通过；请从手机发消息完成端到端验证"
                            : "电脑接收服务与令牌验证通过；请从手机发消息确认实际可达");
                    }
                }
                catch (OperationCanceledException) { return new OperationResult(OperationState.Cancelled, "测试已取消"); }
                catch (WebException ex)
                {
                    if (cancellation.IsCancellationRequested) return new OperationResult(OperationState.Cancelled, "测试已取消");
                    using (HttpWebResponse response = ex.Response as HttpWebResponse)
                        return new OperationResult(OperationState.Failed, response != null ? "服务器返回 HTTP " + (int)response.StatusCode + "，请检查地址与授权"
                            : ex.Status == WebExceptionStatus.Timeout ? "连接超时，请检查路由、防火墙和代理" : "连接失败，请检查网络和服务器设置");
                }
                catch { return new OperationResult(OperationState.Failed, "连接测试失败，请检查网络设置"); }
            }
        }
    }
}
