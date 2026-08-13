using System.Runtime.CompilerServices;

// 允许测试项目访问 internal 成员（分词器、解析器等），保持公开 API 面干净。
[assembly: InternalsVisibleTo("ResearchAssistant.Core.Tests")]
