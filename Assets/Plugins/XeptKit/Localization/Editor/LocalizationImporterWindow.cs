using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using XeptKit.Core;

namespace XeptKit.Localization
{
    /// <summary>
    /// EditorWindow：从 CSV 文件批量导入生成/更新每种语言的 <see cref="LocalizationData"/> ScriptableObject 资产，
    /// 并可生成 <c>LocKeys.cs</c> 键常量类。
    /// 菜单入口：Window → XeptKit → Localization Importer
    /// </summary>
    /// <remarks>
    /// 导入流程：选择 CSV（自动检测语言列、默认全选）→ 选择输出目录（Assets 内）→ 导入。
    /// 编辑期校验（对齐 SceneGroupEditor 校验精神）：表头语言代码经 <see cref="LanguageCodeValidator.IsValid"/> 校验
    /// （非法即中止导入）、重复语言列警告并仅保留首个、重复 Key 警告（完成对话框汇总报告；
    /// 运行时构建字典亦会防御性记录）。日志统一走 <see cref="Log"/> 门面。
    /// </remarks>
    public sealed class LocalizationImporterWindow : EditorWindow
    {
        private const string WindowTitle = "Localization Importer";

        // 数据源
        private ILocalizationDataSource _dataSource = new CsvLocalizationDataSource();

        // 文件 / 目录
        private string _csvFilePath = "";
        private string _outputDir = "Assets/Localization";

        // 语言选择（从 CSV 表头自动填充）
        private string[] _detectedLanguageCodes = System.Array.Empty<string>();
        private bool[] _languageSelections = System.Array.Empty<bool>();

        // 选项
        private bool _generateKeysClass = true;

        // 滚动
        private Vector2 _scrollPos;

        [MenuItem("Window/XeptKit/Localization Importer")]
        private static void ShowWindow()
        {
            var window = GetWindow<LocalizationImporterWindow>(true, WindowTitle);
            window.minSize = new Vector2(420, 520);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            EditorGUILayout.Space();
            DrawFileSelection();
            EditorGUILayout.Space();
            DrawLanguageSelection();
            EditorGUILayout.Space();
            DrawOptions();
            EditorGUILayout.Space();
            DrawImportButton();

            EditorGUILayout.EndScrollView();
        }

        // ===============================================================
        // Sections
        // ===============================================================

        private static void DrawHeader()
        {
            EditorGUILayout.LabelField("Localization CSV Importer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "从 CSV 文件批量生成每种语言的 LocalizationData ScriptableObject 资产。\n" +
                "CSV 格式：第一列为 key，后续每列为一种语言的翻译。第一行为表头。",
                MessageType.Info
            );
        }

        private void DrawFileSelection()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("CSV File", GUILayout.Width(80));
            EditorGUILayout.TextField(_csvFilePath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                var selected = EditorUtility.OpenFilePanel("Select CSV File", "", "csv");
                if (!string.IsNullOrEmpty(selected))
                {
                    _csvFilePath = selected;
                    AutoDetectLanguages();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Output Dir", GUILayout.Width(80));
            EditorGUILayout.TextField(_outputDir);
            if (GUILayout.Button("Select", GUILayout.Width(80)))
            {
                var selected = EditorUtility.OpenFolderPanel("Select Output Directory", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    // 转换为 Assets-relative 路径
                    var dataPath = Application.dataPath;
                    if (selected.StartsWith(dataPath))
                    {
                        _outputDir = "Assets" + selected.Substring(dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog(
                            "Invalid Directory",
                            "请选择 Assets 目录内的文件夹。",
                            "OK"
                        );
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLanguageSelection()
        {
            EditorGUILayout.LabelField("Languages", EditorStyles.boldLabel);

            if (_detectedLanguageCodes.Length == 0)
            {
                EditorGUILayout.HelpBox("请先选择一个 CSV 文件以自动检测语言列。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"从 CSV 表头检测到 {_detectedLanguageCodes.Length} 种语言：");

            EditorGUI.indentLevel++;
            for (int i = 0; i < _detectedLanguageCodes.Length; i++)
            {
                _languageSelections[i] = EditorGUILayout.Toggle(
                    _detectedLanguageCodes[i], _languageSelections[i]);
            }

            // 全选 / 全不选
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All", GUILayout.Width(100)))
            {
                for (int i = 0; i < _languageSelections.Length; i++)
                {
                    _languageSelections[i] = true;
                }
            }
            if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
            {
                for (int i = 0; i < _languageSelections.Length; i++)
                {
                    _languageSelections[i] = false;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);

            _generateKeysClass = EditorGUILayout.Toggle("Generate Keys Class", _generateKeysClass);
        }

        private void DrawImportButton()
        {
            GUI.enabled = ValidateInput();
            if (GUILayout.Button("Import", GUILayout.Height(40)))
            {
                Import();
            }
            GUI.enabled = true;
        }

        // ===============================================================
        // Logic
        // ===============================================================

        private bool ValidateInput()
        {
            if (string.IsNullOrEmpty(_csvFilePath) || !File.Exists(_csvFilePath))
            {
                return false;
            }

            if (string.IsNullOrEmpty(_outputDir))
            {
                return false;
            }

            if (_languageSelections.Length == 0)
            {
                return false;
            }

            bool anySelected = false;
            for (int i = 0; i < _languageSelections.Length; i++)
            {
                if (_languageSelections[i])
                {
                    anySelected = true;
                    break;
                }
            }

            return anySelected;
        }

        private void AutoDetectLanguages()
        {
            try
            {
                var rawData = _dataSource.Read(_csvFilePath);
                _detectedLanguageCodes = rawData.LanguageCodes;
                _languageSelections = new bool[_detectedLanguageCodes.Length];
                // 默认全选
                for (int i = 0; i < _languageSelections.Length; i++)
                {
                    _languageSelections[i] = true;
                }

                Repaint();
            }
            catch (System.Exception ex)
            {
                _detectedLanguageCodes = System.Array.Empty<string>();
                _languageSelections = System.Array.Empty<bool>();
                Log.Error($"[LocalizationImporterWindow] 预览 CSV 失败：{ex.Message}");
            }
        }

        private void Import()
        {
            try
            {
                // 确保输出目录存在
                if (!AssetDatabase.IsValidFolder(_outputDir))
                {
                    CreateDirectoryRecursive(_outputDir);
                }

                var rawData = _dataSource.Read(_csvFilePath);

                // 编辑期校验：表头语言代码（fail-fast，非法代码在源头暴露，不产出死资产）
                for (int i = 0; i < rawData.LanguageCodes.Length; i++)
                {
                    if (!LanguageCodeValidator.IsValid(rawData.LanguageCodes[i]))
                    {
                        throw new System.IO.InvalidDataException(
                            $"CSV 表头含非法语言代码：\"{rawData.LanguageCodes[i]}\"（BCP-47 形状校验失败）。");
                    }
                }

                // 收集所有 key（用于生成 LocKeys）
                var allKeys = new List<string>();
                for (int i = 0; i < rawData.Entries.Length; i++)
                {
                    if (!string.IsNullOrEmpty(rawData.Entries[i].Key))
                    {
                        allKeys.Add(rawData.Entries[i].Key);
                    }
                }

                // 编辑期校验：重复 Key 警告（运行时构建字典亦会防御，此处于源头提示并在完成对话框汇总报告）
                int duplicateKeyCount = 0;
                var seenKeys = new HashSet<string>(System.StringComparer.Ordinal);
                foreach (var rawEntry in rawData.Entries)
                {
                    if (string.IsNullOrEmpty(rawEntry.Key))
                    {
                        continue;
                    }

                    if (!seenKeys.Add(rawEntry.Key))
                    {
                        duplicateKeyCount++;
                    }
                }

                if (duplicateKeyCount > 0)
                {
                    Log.Warning(
                        $"[LocalizationImporterWindow] 检测到 {duplicateKeyCount} 个重复 Key" +
                        "（运行时后者覆盖前者），请检查 CSV。");
                }

                // 每种选中的语言创建一个/更新 LocalizationData SO（重复语言列仅保留首个，编辑期警告）
                int createdCount = 0;
                var seenCodes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                for (int langIdx = 0; langIdx < rawData.LanguageCodes.Length; langIdx++)
                {
                    var languageCode = rawData.LanguageCodes[langIdx];
                    if (!seenCodes.Add(languageCode))
                    {
                        Log.Warning(
                            $"[LocalizationImporterWindow] 表头语言代码 \"{languageCode}\" 重复，仅保留首个出现。");
                        continue;
                    }

                    if (langIdx >= _languageSelections.Length || !_languageSelections[langIdx])
                    {
                        continue;
                    }

                    var entries = new LocalizationEntry[rawData.Entries.Length];
                    for (int entryIdx = 0; entryIdx < rawData.Entries.Length; entryIdx++)
                    {
                        var rawEntry = rawData.Entries[entryIdx];
                        var value = langIdx < rawEntry.Values.Length
                            ? rawEntry.Values[langIdx]
                            : string.Empty;

                        entries[entryIdx] = new LocalizationEntry
                        {
                            Key = rawEntry.Key,
                            Value = value
                        };
                    }

                    var assetPath = $"{_outputDir}/Localization_{languageCode}.asset";

                    // 尝试加载已有资产进行更新，否则创建新资产
                    var existingAsset = AssetDatabase.LoadAssetAtPath<LocalizationData>(assetPath);
                    var localizationData = existingAsset != null
                        ? existingAsset
                        : ScriptableObject.CreateInstance<LocalizationData>();

                    localizationData.LanguageCode = languageCode;
                    localizationData.FallbackLanguageCode =
                        FindFallbackLanguage(languageCode, rawData.LanguageCodes);
                    localizationData.Entries = entries;

                    if (existingAsset == null)
                    {
                        AssetDatabase.CreateAsset(localizationData, assetPath);
                    }
                    else
                    {
                        EditorUtility.SetDirty(localizationData);
                    }

                    createdCount++;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 生成 LocKeys.cs
                int generatedKeys = 0;
                if (_generateKeysClass)
                {
                    generatedKeys = LocKeysGenerator.Generate(_outputDir, allKeys);
                }

                EditorUtility.DisplayDialog(
                    "Import Complete",
                    $"成功生成/更新 {createdCount} 个 LocalizationData 资产。" +
                    (_generateKeysClass
                        ? $"\nLocKeys.cs 已生成（{allKeys.Count} 个键，{generatedKeys} 个常量）。"
                        : "") +
                    (duplicateKeyCount > 0
                        ? $"\n重复 Key：{duplicateKeyCount} 个（见 Console 警告）。"
                        : ""),
                    "OK"
                );
            }
            catch (System.Exception ex)
            {
                Log.Error($"[LocalizationImporterWindow] 导入失败：{ex}");
                EditorUtility.DisplayDialog("Import Failed", ex.Message, "OK");
            }
        }

        /// <summary>
        /// 查找回退语言：优先英文，其次第一个不同于目标语言的语言代码。
        /// </summary>
        private static string FindFallbackLanguage(string targetLanguage, string[] allLanguages)
        {
            // 优先 "en"
            foreach (var lang in allLanguages)
            {
                if (string.Equals(lang, "en", System.StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(lang, targetLanguage, System.StringComparison.OrdinalIgnoreCase))
                {
                    return lang;
                }
            }

            // 其次第一个不同于目标的语言
            foreach (var lang in allLanguages)
            {
                if (!string.Equals(lang, targetLanguage, System.StringComparison.OrdinalIgnoreCase))
                {
                    return lang;
                }
            }

            // 没有其他语言，回退设为自己
            return targetLanguage;
        }

        /// <summary>
        /// 递归创建 Assets 目录下的子文件夹。
        /// </summary>
        private static void CreateDirectoryRecursive(string assetPath)
        {
            // assetPath 格式："Assets/Localization/SubDir"
            var parts = assetPath.Split('/');
            var current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                var parent = current;
                current = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(current))
                {
                    AssetDatabase.CreateFolder(parent, parts[i]);
                }
            }
        }
    }
}
