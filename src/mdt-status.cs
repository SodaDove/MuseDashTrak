using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MdtStatus
{
    // Prints one line describing the current Muse Dash play state for
    // NekoTrak's custom status text:  ${C:\...\mdt-status.exe}
    //
    // Written against .NET Framework 4.8 / C# 5 so it can be compiled with
    // the csc.exe that ships with Windows.
    internal static class Program
    {
        private static string Dir
        {
            get
            {
                string overridden = Environment.GetEnvironmentVariable("MUSEDASHTRAK_DIR");
                if (!string.IsNullOrEmpty(overridden))
                    return overridden;
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MuseDashTrak");
            }
        }

        private static int Main(string[] args)
        {
            bool verbose = args.Length > 0 && (args[0] == "--check" || args[0] == "-v");
            try
            {
                string statusPath = Path.Combine(Dir, "status.json");
                if (!File.Exists(statusPath))
                    return Finish(verbose ? "(no status.json yet - Muse Dash has not run with the mod)" : "", verbose);

                string json = File.ReadAllText(statusPath, Encoding.UTF8);
                string state = GetStr(json, "state");
                long updated = (long)GetNum(json, "updated", 0);

                long age = NowUnix() - updated;
                if (verbose)
                {
                    Console.Error.WriteLine("state=" + state + " age=" + age + "s");
                    Console.Error.WriteLine(json);
                }

                if (state == "idle" || state.Length == 0)
                    return Finish("", verbose);
                // Mod writes every ~0.5s; anything older than 20s means the
                // game was closed without a clean shutdown.
                if (age > 20)
                    return Finish("", verbose);

                string text = Format(ReadFormat(), json, state);
                return Finish(text, verbose);
            }
            catch (Exception e)
            {
                if (verbose)
                    Console.Error.WriteLine(e.ToString());
                return Finish("", verbose);
            }
        }

        private static int Finish(string text, bool verbose)
        {
            if (verbose)
            {
                Console.Error.WriteLine("OUT: " + text);
                return 0;
            }
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            using (Stream stdout = Console.OpenStandardOutput())
            {
                stdout.Write(bytes, 0, bytes.Length);
                stdout.Flush();
            }
            return 0;
        }

        private static string[] ReadFormat()
        {
            // format.txt: line 1 = playing template, line 2 = result template.
            string path = Path.Combine(Dir, "format.txt");
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                if (lines.Length >= 2 && lines[0].Trim().Length > 0 && lines[1].Trim().Length > 0)
                    return new[] { lines[0], lines[1] };
            }
            return new[]
            {
                "🔥 {song}{lv} {acc}% {score}{combo}",
                "🏁 {song}{lv} {acc}% {score}"
            };
        }

        private static string Format(string[] templates, string json, string state)
        {
            string song = GetStr(json, "song");
            string level = GetStr(json, "level");
            double diff = GetNum(json, "diff", 0);
            double acc = GetNum(json, "acc", 0);
            double score = GetNum(json, "score", 0);
            double combo = GetNum(json, "combo", 0);
            double elapsed = GetNum(json, "elapsed", 0);
            if (song.Length == 0)
                return "";

            string t = state == "result" ? templates[1] : templates[0];
            string comboText = combo > 0 ? (" · " + ((long)combo) + "x") : "";
            string elapsedText = FormatTime(elapsed);

            t = t.Replace("{song}", song)
                 .Replace("{diffName}", DiffName(diff))
                 .Replace("{lv}", level.Length > 0 ? (" Lv." + level) : "")
                 .Replace("{level}", level)
                 .Replace("{acc}", acc.ToString("0.00", CultureInfo.InvariantCulture))
                 .Replace("{score}", ((long)score).ToString(CultureInfo.InvariantCulture))
                 .Replace("{combo}", comboText)
                 .Replace("{combos}", combo > 0 ? (((long)combo) + "x") : "")
                 .Replace("{elapsed}", elapsedText)
                 .Replace("{state}", state);

            return Flatten(t);
        }

        private static string Flatten(string s)
        {
            // Steam rich presence is a single line: collapse newlines/tabs and
            // trim runs of spaces.
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (char c in s)
            {
                char ch = (c == '\r' || c == '\n' || c == '\t') ? ' ' : c;
                if (ch == ' ')
                {
                    if (space) continue;
                    space = true;
                }
                else
                {
                    space = false;
                }
                sb.Append(ch);
            }
            return sb.ToString().Trim();
        }

        private static string DiffName(double diff)
        {
            switch ((int)diff)
            {
                case 1: return "EASY";
                case 2: return "HARD";
                case 3: return "MASTER";
                case 4: return "HIDDEN";
                case 5: return "TOUHOU";
                default: return "";
            }
        }

        private static string FormatTime(double seconds)
        {
            int s = (int)Math.Max(0, seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        private static long NowUnix()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        // --- tiny JSON helpers (flat object only) -------------------------

        private static int FindKey(string json, string key)
        {
            string needle = "\"" + key + "\"";
            int i = json.IndexOf(needle, StringComparison.Ordinal);
            if (i < 0)
                return -1;
            i = json.IndexOf(':', i + needle.Length);
            if (i < 0)
                return -1;
            return i + 1;
        }

        private static string GetStr(string json, string key)
        {
            int i = FindKey(json, key);
            if (i < 0)
                return "";
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length || json[i] != '"')
                return "";
            i++;
            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '"') break;
                if (c == '\\' && i < json.Length)
                {
                    char e = json[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= json.Length)
                            {
                                sb.Append((char)Convert.ToInt32(json.Substring(i, 4), 16));
                                i += 4;
                            }
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static double GetNum(string json, string key, double fallback)
        {
            int i = FindKey(json, key);
            if (i < 0)
                return fallback;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-' || json[i] == '+' || json[i] == '.')) i++;
            double v;
            if (i > start && double.TryParse(json.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }
    }
}
