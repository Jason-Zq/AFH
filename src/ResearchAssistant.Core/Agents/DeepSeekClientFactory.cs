using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace ResearchAssistant.Core.Agents;

/// <summary>
/// DeepSeek 客户端工厂。全项目统一从这里创建 IChatClient，
/// 端点/模型等细节只在这一个地方维护（M0 已验证 /v1 端点 + 工具调用可用）。
/// </summary>
public static class DeepSeekClientFactory
{
    public const string DefaultEndpoint = "https://api.deepseek.com/v1";
    public const string DefaultModel = "deepseek-chat";

    /// <summary>创建客户端。apiKey/endpoint 缺省时回退到环境变量（DEEPSEEK_API_KEY / DEEPSEEK_ENDPOINT / DEEPSEEK_MODEL）。</summary>
    /// <exception cref="InvalidOperationException">未提供 apiKey 且环境变量 DEEPSEEK_API_KEY 也未设置时抛出。</exception>
    public static IChatClient Create(string? model = null, string? apiKey = null, string? endpoint = null)
    {
        apiKey ??= Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
            ?? throw new InvalidOperationException("未找到 DeepSeek API key：请配置 DeepSeek:ApiKey（appsettings.json）或环境变量 DEEPSEEK_API_KEY。");
        endpoint ??= Environment.GetEnvironmentVariable("DEEPSEEK_ENDPOINT") ?? DefaultEndpoint;

        return new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
            .GetChatClient(model ?? Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? DefaultModel)
            .AsIChatClient();
    }
}
