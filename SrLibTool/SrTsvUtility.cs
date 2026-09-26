using System;
using System.Collections.Generic;
using System.Linq;

namespace SrLibTool
{
    public static class SrTsvUtility
    {
        public static List<List<string>> ParseTsv(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            return ParseTsv(lines);
        }

        public static List<List<string>> ParseTsv(List<string> lines)
        {
            var rows = new List<List<string>>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                rows.Add(line.Split('\t').ToList());
            }
            return rows;
        }
    }
}
