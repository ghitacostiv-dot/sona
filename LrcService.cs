using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SONA.Services
{
    public class LrcLine
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; } = "";
    }

    public static class LrcParser
    {
        private static readonly Regex _lrcRegex = new Regex(@"^\[(\d+):(\d+)\.(\d+)\](.*)$");

        public static List<LrcLine> Parse(string lrcContent)
        {
            var lines = new List<LrcLine>();
            if (string.IsNullOrWhiteSpace(lrcContent)) return lines;

            var rawLines = lrcContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in rawLines)
            {
                var match = _lrcRegex.Match(line.Trim());
                if (match.Success)
                {
                    int mins = int.Parse(match.Groups[1].Value);
                    int secs = int.Parse(match.Groups[2].Value);
                    int ms = int.Parse(match.Groups[3].Value);
                    string text = match.Groups[4].Value.Trim();

                    // Handle different precision of ms
                    if (match.Groups[3].Value.Length == 2) ms *= 10;

                    var time = new TimeSpan(0, 0, mins, secs, ms);
                    lines.Add(new LrcLine { Time = time, Text = text });
                }
            }
            return lines.OrderBy(l => l.Time).ToList();
        }

        public static int GetCurrentLineIndex(List<LrcLine> lines, TimeSpan currentTime)
        {
            if (lines == null || lines.Count == 0) return -1;
            
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (currentTime >= lines[i].Time)
                    return i;
            }
            return -1;
        }
    }
}
