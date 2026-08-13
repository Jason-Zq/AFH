namespace ResearchAssistant.Core.Search;

/// <summary>定位 data/corpus 目录：从指定起点向上逐级查找，兼容 CLI、Web、bin 等不同工作目录。</summary>
public static class CorpusLocator
{
    public static string Find(string? startDirectory = null)
    {
        var dir = new DirectoryInfo(startDirectory ?? Environment.CurrentDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "corpus");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException($"未找到 data/corpus 目录（从 {startDirectory ?? Environment.CurrentDirectory} 向上查找失败）");
    }
}
