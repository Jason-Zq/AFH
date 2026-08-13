using System.Text.RegularExpressions;

namespace ResearchAssistant.Core.Search;

/// <summary>
/// 本地 Markdown 语料库的轻量 BM25 检索。
/// 不依赖 embedding 模型（DeepSeek 无 embedding 接口），
/// 中文按二元组（bigram）切分、英文按词切分，混合查询也能命中。
/// 向量检索留作后续扩展。
/// </summary>
public sealed partial class LocalCorpusSearch
{
    // BM25 经典参数
    private const double K1 = 1.5;
    private const double B = 0.75;

    private readonly List<CorpusChunk> _chunks;
    private readonly Dictionary<string, double> _idf = new();
    private readonly double _avgLength;

    private LocalCorpusSearch(List<CorpusChunk> chunks)
    {
        _chunks = chunks;
        _avgLength = chunks.Count == 0 ? 0 : chunks.Average(c => c.Tokens.Count);

        // 计算逆文档频率：idf = ln(1 + (N - df + 0.5) / (df + 0.5))
        var docFreq = new Dictionary<string, int>();
        foreach (var chunk in chunks)
        {
            foreach (var token in chunk.Tokens.Distinct())
            {
                docFreq[token] = docFreq.GetValueOrDefault(token) + 1;
            }
        }
        foreach (var (token, df) in docFreq)
        {
            _idf[token] = Math.Log(1 + (chunks.Count - df + 0.5) / (df + 0.5));
        }
    }

    /// <summary>加载目录下全部 .md 文件并建立索引。目录不存在或为空时返回空索引（Search 返回空列表）。</summary>
    public static LocalCorpusSearch Load(string corpusDirectory)
    {
        var chunks = new List<CorpusChunk>();
        if (!Directory.Exists(corpusDirectory))
        {
            return new LocalCorpusSearch(chunks);
        }
        foreach (var file in Directory.EnumerateFiles(corpusDirectory, "*.md").OrderBy(f => f))
        {
            chunks.AddRange(Chunk(file, File.ReadAllText(file)));
        }
        return new LocalCorpusSearch(chunks);
    }

    /// <summary>从内存文档列表（如数据库读出的语料）建立索引，与文件目录版共用同一切块/索引逻辑。</summary>
    public static LocalCorpusSearch Load(IEnumerable<(string Name, string Content)> documents)
    {
        var chunks = new List<CorpusChunk>();
        foreach (var (name, content) in documents)
        {
            chunks.AddRange(Chunk(name, content));
        }
        return new LocalCorpusSearch(chunks);
    }

    public int ChunkCount => _chunks.Count;

    /// <summary>检索，返回按 BM25 得分降序的前 maxResults 条。</summary>
    public IReadOnlyList<SearchHit> Search(string query, int maxResults = 5)
    {
        var queryTokens = Tokenize(query).ToList();
        if (queryTokens.Count == 0 || _chunks.Count == 0)
        {
            return [];
        }

        var scored = new List<(CorpusChunk Chunk, double Score)>();
        foreach (var chunk in _chunks)
        {
            double score = 0;
            foreach (var token in queryTokens.Distinct())
            {
                if (!_idf.TryGetValue(token, out var idf))
                {
                    continue;
                }
                var tf = chunk.Tokens.Count(t => t == token);
                if (tf == 0)
                {
                    continue;
                }
                var norm = K1 * (1 - B + B * chunk.Tokens.Count / _avgLength);
                score += idf * (tf * (K1 + 1)) / (tf + norm);
            }
            if (score > 0)
            {
                scored.Add((chunk, score));
            }
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Take(maxResults)
            .Select(s => new SearchHit(s.Chunk.Heading, s.Chunk.FileName, s.Chunk.Text, "Local"))
            .ToList();
    }

    /// <summary>按标题/段落把 Markdown 切成块，块上保留标题与文件名作为上下文。</summary>
    internal static IEnumerable<CorpusChunk> Chunk(string filePath, string markdown)
    {
        var fileName = Path.GetFileName(filePath);
        var heading = fileName;
        var paragraph = new List<string>();

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (HeadingRegex().IsMatch(line))
            {
                foreach (var c in Flush()) yield return c;
                heading = line.TrimStart('#', ' ');
            }
            else if (string.IsNullOrWhiteSpace(line))
            {
                foreach (var c in Flush()) yield return c;
            }
            else if (!line.StartsWith("<!--"))
            {
                paragraph.Add(line);
            }
        }
        foreach (var c in Flush()) yield return c;

        IEnumerable<CorpusChunk> Flush()
        {
            if (paragraph.Count == 0) yield break;
            var text = string.Join(' ', paragraph);
            paragraph.Clear();
            if (text.Length >= 30)  // 过滤过短碎块（列表骨架、装饰行等）
            {
                yield return new CorpusChunk(fileName, heading, text, Tokenize(text + " " + heading).ToList());
            }
        }
    }

    /// <summary>分词：英文/数字按词，CJK 字符按二元组。单字查询时退化为单字。</summary>
    internal static IEnumerable<string> Tokenize(string text)
    {
        foreach (Match match in WordRegex().Matches(text.ToLowerInvariant()))
        {
            var word = match.Value;
            if (IsCjk(word))
            {
                if (word.Length == 1)
                {
                    yield return word;
                }
                for (var i = 0; i < word.Length - 1; i++)
                {
                    yield return word.Substring(i, 2);
                }
            }
            else
            {
                yield return word;
            }
        }
    }

    private static bool IsCjk(string s) => s.All(c => c is >= '一' and <= '鿿');

    [GeneratedRegex(@"[一-鿿]+|[a-z0-9]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"^#{1,6}\s")]
    private static partial Regex HeadingRegex();

    internal sealed record CorpusChunk(string FileName, string Heading, string Text, List<string> Tokens);
}
