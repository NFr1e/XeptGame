using System.Runtime.CompilerServices;

// 使编辑器程序集（XeptKit.Editor）可访问运行时程序集（XeptKit）的 internal 成员
// （EditorAssetLoader 需构造 AssetHandle<T>，其构造器为 internal）。
[assembly: InternalsVisibleTo("XeptKit.Editor")]
