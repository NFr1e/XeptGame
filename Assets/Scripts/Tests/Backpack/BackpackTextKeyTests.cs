using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using XeptGame.UI.Backpack;

namespace XeptGame.Tests
{
    /// <summary>
    /// 背包文案键 ↔ CSV 的双向覆盖（Backpack_UI_Design.md B5；做法沿用 <c>InteractionPromptKeyTests</c>）：
    /// 代码只存键、句子在 CSV，缺任一侧界面都会显示错东西（回退成键名），所以两侧都要断言。
    /// </summary>
    public sealed class BackpackTextKeyTests
    {
        private const string CsvRelativePath = "GameData/Localization/Items.zh-CN-en.csv";

        [Test]
        public void 背包的每个文案键都在CSV里有中英取值()
        {
            var rows = ReadCsv();
            foreach (var key in AllKeys())
            {
                Assert.IsTrue(rows.ContainsKey(key), "背包文案键在 CSV 里没有对应行：" + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(rows[key].Zh), key + " 的 zh-CN 取值为空。");
                Assert.IsFalse(string.IsNullOrWhiteSpace(rows[key].En), key + " 的 en 取值为空。");
            }
        }

        [Test]
        public void CSV里的背包键都有代码引用()
        {
            var used = new HashSet<string>(AllKeys());
            var orphans = new List<string>();

            foreach (var pair in ReadCsv())
            {
                if (pair.Key.StartsWith("backpack.") && !used.Contains(pair.Key))
                {
                    orphans.Add(pair.Key);
                }
            }

            Assert.IsEmpty(orphans, "CSV 里存在没有任何代码引用的背包键：" + string.Join("、", orphans));
        }

        /// <summary>
        /// 键集合从 <see cref="BackpackTextKeys"/> 的公开常量反射而来——新增常量后<b>忘改 CSV 会立刻红</b>，
        /// 这正是不把键写成散落字符串字面量的意义。
        /// </summary>
        private static List<string> AllKeys()
        {
            var keys = new List<string>();
            var fields = typeof(BackpackTextKeys).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                {
                    keys.Add((string)field.GetRawConstantValue());
                }
            }

            Assert.IsNotEmpty(keys, "BackpackTextKeys 没有取到任何常量——反射方式失效了。");
            return keys;
        }

        private static Dictionary<string, Row> ReadCsv()
        {
            var path = Path.Combine(Application.dataPath, CsvRelativePath);
            Assert.IsTrue(File.Exists(path), "找不到本地化 CSV：" + path);

            var lines = File.ReadAllLines(path);
            var rows = new Dictionary<string, Row>();
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var cells = line.Split(',');
                if (cells.Length < 3)
                {
                    continue;
                }

                rows[cells[0].Trim()] = new Row(cells[1].Trim(), cells[2].Trim());
            }

            return rows;
        }

        private readonly struct Row
        {
            public readonly string Zh;
            public readonly string En;

            public Row(string zh, string en)
            {
                Zh = zh;
                En = en;
            }
        }
    }
}
