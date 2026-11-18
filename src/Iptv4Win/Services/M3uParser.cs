using System.Text.RegularExpressions;
using Iptv4Win.Models;

namespace Iptv4Win.Services;

public static class M3uParser
{
    static readonly Regex Attr = new("([\\w-]+)=\"([^\"]*)\"", RegexOptions.Compiled);

    public static List<Channel> Parse(string text)
    {
        var list = new List<Channel>();
        string? name = null, logo = null, group = null;
        var n = 1;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                string? tvgName = null;
                foreach (Match m in Attr.Matches(line))
                {
                    var key = m.Groups[1].Value.ToLowerInvariant();
                    var val = m.Groups[2].Value;
                    if (key == "tvg-logo") logo = val;
                    else if (key == "group-title") group = val;
                    else if (key == "tvg-name") tvgName = val;
                }
                var comma = line.LastIndexOf(',');
                name = comma >= 0 ? line[(comma + 1)..].Trim() : null;
                if (string.IsNullOrWhiteSpace(name)) name = tvgName;
            }
            else if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase))
            {
                group = line[8..].Trim();
            }
            else if (!line.StartsWith('#'))
            {
                list.Add(new Channel
                {
                    Number = n++,
                    Name = name ?? line,
                    Group = group ?? "",
                    LogoUrl = logo,
                    StreamUrl = line
                });
                name = logo = group = null;
            }
        }
        return list;
    }
}
