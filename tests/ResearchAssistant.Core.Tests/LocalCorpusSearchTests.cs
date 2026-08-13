using ResearchAssistant.Core.Search;

namespace ResearchAssistant.Core.Tests;

public class LocalCorpusSearchTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "corpus-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    // ---------- 分词器 ----------

    [Fact]
    public void Tokenize_中文按二元组切分()
    {
        var tokens = LocalCorpusSearch.Tokenize("智能体").ToList();
        Assert.Equal(["智能", "能体"], tokens);
    }

    [Fact]
    public void Tokenize_英文按词切分且转小写()
    {
        var tokens = LocalCorpusSearch.Tokenize("ReAct Pattern").ToList();
        Assert.Equal(["react", "pattern"], tokens);
    }

    [Fact]
    public void Tokenize_中英文混合查询()
    {
        var tokens = LocalCorpusSearch.Tokenize("什么是 RAG 检索").ToList();
        Assert.Contains("rag", tokens);
        Assert.Contains("什么", tokens);
        Assert.Contains("检索", tokens);
    }

    // ---------- 切块 ----------

    [Fact]
    public void Chunk_按标题与段落切分并保留上下文()
    {
        var md = """
            # ReAct 模式

            ReAct 是一种将推理与行动交替进行的智能体架构模式，由谷歌研究团队在 2022 年提出。

            ## 工作流程

            智能体在每一步先生成一段思考（Thought），再决定调用哪个工具（Action），然后观察结果（Observation），循环往复直到得出最终答案。
            """;

        var chunks = LocalCorpusSearch.Chunk("react.md", md).ToList();

        Assert.Equal(2, chunks.Count);
        Assert.Equal("ReAct 模式", chunks[0].Heading);
        Assert.Equal("工作流程", chunks[1].Heading);
        Assert.All(chunks, c => Assert.Equal("react.md", c.FileName));
    }

    [Fact]
    public void Chunk_过滤过短碎块()
    {
        var md = "# 标题\n\n短。\n\n这一段的内容长度足够超过三十个字符，因此应该被保留为一个有效的检索块。";
        var chunks = LocalCorpusSearch.Chunk("a.md", md).ToList();
        Assert.Single(chunks);
    }

    // ---------- 检索排序 ----------

    [Fact]
    public void Search_相关文档排在前面()
    {
        WriteFile("react.md", "# ReAct 模式\n\nReAct 将推理（Reasoning）与行动（Acting）结合，智能体交替生成思考与工具调用，逐步逼近答案。");
        WriteFile("cooking.md", "# 红烧肉做法\n\n五花肉切块焯水，冰糖炒糖色，加料酒生抽老抽小火慢炖一小时，大火收汁即可。");
        var corpus = LocalCorpusSearch.Load(_dir);

        var hits = corpus.Search("ReAct 智能体模式");

        Assert.NotEmpty(hits);
        Assert.Equal("react.md", hits[0].Source);
    }

    [Fact]
    public void Load_内存文档列表与文件目录效果一致()
    {
        var docs = new[]
        {
            ("react.md", "# ReAct 模式\n\nReAct 将推理（Reasoning）与行动（Acting）结合，智能体交替生成思考与工具调用，逐步逼近答案。"),
            ("cooking.md", "# 红烧肉做法\n\n五花肉切块焯水，冰糖炒糖色，加料酒生抽老抽小火慢炖一小时，大火收汁即可。"),
        };
        var corpus = LocalCorpusSearch.Load(docs.AsEnumerable());

        var hits = corpus.Search("ReAct 智能体模式");

        Assert.NotEmpty(hits);
        Assert.Equal("react.md", hits[0].Source);
    }

    [Fact]
    public void Search_空查询或空库返回空列表()
    {
        var empty = LocalCorpusSearch.Load(_dir); // 目录不存在 => 空库
        Assert.Empty(empty.Search("任何东西"));

        WriteFile("a.md", "# 文档\n\n这是一段长度足够的内容，用于验证空查询不会导致异常或误判。");
        var corpus = LocalCorpusSearch.Load(_dir);
        Assert.Empty(corpus.Search(""));
        Assert.Empty(corpus.Search("！！！"));
    }

    private void WriteFile(string name, string content)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, name), content);
    }
}
