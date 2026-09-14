using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using XeptGame.Interaction;

namespace XeptGame.Tests
{
    /// <summary>
    /// 文案键 ↔ CSV 的双向覆盖（Interaction_Behaviour_Design.md §5）：代码只存键，
    /// 句子在 CSV；缺任一侧都会让界面显示错东西，所以两侧都要断言。
    /// </summary>
    public sealed class InteractionPromptKeyTests
    {
        private const string CsvRelativePath = "GameData/Localization/Items.zh-CN-en.csv";

        [Test]
        public void 每个行为的文案键都在CSV里有中英取值()
        {
            var rows = ReadCsv();
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                var behaviour = InteractionBehaviourCatalog.All[i];
                Assert.IsTrue(rows.ContainsKey(behaviour.PromptKey),
                    behaviour.GetType().Name + " 的文案键在 CSV 里没有对应行：" + behaviour.PromptKey);
                Assert.IsFalse(string.IsNullOrWhiteSpace(rows[behaviour.PromptKey].Zh),
                    behaviour.PromptKey + " 的 zh-CN 取值为空。");
                Assert.IsFalse(string.IsNullOrWhiteSpace(rows[behaviour.PromptKey].En),
                    behaviour.PromptKey + " 的 en 取值为空。");
            }
        }

        [Test]
        public void CSV里的交互键都有对应行为()
        {
            var rows = ReadCsv();
            var used = new HashSet<string>();
            for (int i = 0; i < InteractionBehaviourCatalog.All.Count; i++)
            {
                used.Add(InteractionBehaviourCatalog.All[i].PromptKey);
            }

            var orphans = new List<string>();
            foreach (var pair in rows)
            {
                if (pair.Key.StartsWith("interaction.") && !used.Contains(pair.Key))
                {
                    orphans.Add(pair.Key);
                }
            }

            Assert.IsEmpty(orphans, "CSV 里存在没有任何行为使用的交互键：" + string.Join("、", orphans));
        }

        /// <summary>读 CSV 原文（唯一真源）——不读生成后的语言包资产，避免"资产里改了但 CSV 没改"漏检。</summary>
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
