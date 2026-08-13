// M0 冒烟测试：验证 DeepSeek 连通性、工具调用、Harness 三个关键假设。
// 用法：在仓库根目录执行  set -a; source .env; set +a; cd tools/SmokeTest; dotnet run
// 退出码：0 = 全部通过；1 = 存在失败项。

using System.ClientModel;
using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
    ?? throw new InvalidOperationException("DEEPSEEK_API_KEY 未设置");
var model = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? "deepseek-chat";

// DeepSeek 官方文档的 OpenAI 兼容 base_url 带 /v1；但有资料显示 Agent Framework 场景下不带 /v1。
// 两个候选端点都试，报告哪个可用——这正是冒烟测试的价值。
var endpoints = new[] { "https://api.deepseek.com/v1", "https://api.deepseek.com" };

IChatClient CreateClient(string endpoint) =>
    new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(endpoint) })
        .GetChatClient(model)
        .AsIChatClient();

var results = new List<(string Name, bool Pass, string Detail)>();

// ---------- 测试 1：基础连通 ----------
IChatClient? workingClient = null;
foreach (var ep in endpoints)
{
    try
    {
        var client = CreateClient(ep);
        var response = await client.GetResponseAsync("用一句话回答：1+1 等于几？");
        results.Add(("基础连通", true, $"端点 {ep} 可用，模型 {model} 回复：{response.Text?.Trim()}"));
        workingClient = client;
        break;
    }
    catch (Exception ex)
    {
        results.Add(("基础连通", false, $"端点 {ep} 失败：{ex.GetType().Name}: {ex.Message}"));
    }
}

if (workingClient is null)
{
    Report(results);
    return 1;
}

// ---------- 测试 2：工具调用（Function Calling）----------
// 已知风险：早期 DeepSeek + OpenAI SDK 组合在工具调用时曾报 400，必须实测。
var toolCalled = false;

[Description("获取指定城市的当前天气（返回模拟数据）")]
string GetWeather([Description("城市名称，例如：北京")] string city)
{
    toolCalled = true;
    return $"{city}：晴，28℃，东南风2级（模拟数据）";
}

try
{
    // 显式挂上函数调用中间件，这是最成熟的路径。
    IChatClient clientWithTools = workingClient.AsBuilder().UseFunctionInvocation().Build();
    AIAgent agent = clientWithTools.AsAIAgent(
        instructions: "你是一个助手。被问到天气时必须调用 GetWeather 工具，不要自己编造。",
        name: "WeatherBot",
        tools: [AIFunctionFactory.Create(GetWeather)]);

    var response = await agent.RunAsync("北京现在天气怎么样？");
    results.Add(("工具调用", toolCalled, toolCalled
        ? $"工具被调用，最终回复：{response.Text?.Trim()}"
        : $"模型未调用工具，直接回复：{response.Text?.Trim()}"));
}
catch (Exception ex)
{
    results.Add(("工具调用", false, $"{ex.GetType().Name}: {ex.Message}"));
}

// ---------- 测试 3：Harness（AsHarnessAgent）----------
// 注意：Harness 默认启用的 HostedWebSearchTool 是服务端托管工具，DeepSeek 不支持，必须禁用。
try
{
    toolCalled = false;
    var harnessAgent = workingClient.AsHarnessAgent(new HarnessAgentOptions
    {
        Name = "HarnessBot",
        DisableWebSearch = true,      // DeepSeek 无服务端搜索工具
        DisableFileMemory = true,     // 冒烟测试不在磁盘留痕迹
        ChatOptions = new()
        {
            Instructions = "你是一个研究助手。被问到天气时必须调用 GetWeather 工具。",
            Tools = [AIFunctionFactory.Create(GetWeather)],
        },
    });

    var response = await harnessAgent.RunAsync("上海天气如何？顺便说说你用了什么工具。");
    results.Add(("Harness 接入", toolCalled, toolCalled
        ? $"Harness 跑通且工具被调用，回复：{response.Text?.Trim()}"
        : $"Harness 跑通但工具未调用，回复：{response.Text?.Trim()}"));
}
catch (Exception ex)
{
    results.Add(("Harness 接入", false, $"{ex.GetType().Name}: {ex.Message}"));
}

Report(results);
return results.All(r => r.Pass) ? 0 : 1;

static void Report(List<(string Name, bool Pass, string Detail)> results)
{
    Console.WriteLine();
    Console.WriteLine("================ M0 冒烟测试报告 ================");
    foreach (var (name, pass, detail) in results)
    {
        Console.WriteLine($"[{(pass ? "PASS" : "FAIL")}] {name}");
        Console.WriteLine($"      {detail}");
    }
    Console.WriteLine("=================================================");
}
