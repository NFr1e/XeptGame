using System.Runtime.CompilerServices;

// 使测试程序集（XeptKit.Tests）可访问 Scenes 模块的 internal 成员
// （SceneLoadRequest 等 internal 请求类型——加载失败/取消的句柄完成语义验证；
// 仅测试，Editor 程序集不依赖）。
[assembly: InternalsVisibleTo("XeptKit.Tests")]
