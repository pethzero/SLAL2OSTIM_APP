using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SLAL2OStim;

/// <summary>
/// SLAL -> OStim converter for OCreatures (port of slal2ostim_ocreatures.py).
///  1. copies the whole pack (meshes, SLAnims, Textures ...)
///  2. converts every FNIS_*_List.txt to "b -Tn" format with OCreatures event numbering
///  3. runs FNIS for Modders -> FNIS_[mod]_Behavior.hkx
///  4. writes OStim scenes: SKSE/Plugins/OStim/scenes/OCreatures/OCr[Race]/[code]/[Prefix][Race]/...
/// </summary>
public class Converter
{
    public Action<string> Log = Console.WriteLine;
    public string FnisExe = DefaultFnisExe;
    public bool GenBehaviors = true;
    public string Prefix = "";

    public static string DefaultFnisExe => Path.Combine(AppContext.BaseDirectory,
        "FNIS", "tools", "GenerateFNIS_for_Modders", "GenerateFNISforModders.exe");

    // ------------------------------------------------------------ OCreatures data
    // (from OCreatures 1.4: SKSE/Plugins/OStim/scenes/OCreatures + actor properties)

    static readonly string[] OcrRaces =
    {
        "AtronachFlame", "AtronachFrost", "AtronachStorm", "Bear", "Boar", "BoarMount",
        "Canine", "Chaurus", "ChaurusFlyer", "ChaurusReaper", "Deer", "Dragon",
        "Dragonpriest", "Draugr", "DwarvenBallista", "DwarvenCenturion", "DwarvenSphere",
        "DwarvenSpider", "Falmer", "Fox", "Gargoyle", "Giant", "Goat", "Hagraven",
        "Horker", "Horse", "IceWraith", "Lurker", "Mammoth", "Netch", "Rabbit",
        "Riekling", "SabreCat", "Scrib", "Seeker", "Skeever", "Slaughterfish", "Spider",
        "SpiderGiant", "SpiderLarge", "Spriggan", "Troll", "VampireLord", "Werewolf",
    };

    static readonly Dictionary<string, string> RaceAliases = new()
    {
        ["canines"] = "Canine", ["dogs"] = "Canine", ["dog"] = "Canine", ["wolves"] = "Canine",
        ["wolf"] = "Canine", ["ashhoppers"] = "Scrib", ["ashhopper"] = "Scrib",
        ["chaurushunters"] = "ChaurusFlyer", ["chaurushunter"] = "ChaurusFlyer",
        ["giantspiders"] = "SpiderGiant", ["giantspider"] = "SpiderGiant",
        ["largespiders"] = "SpiderLarge", ["largespider"] = "SpiderLarge",
        ["boarsany"] = "Boar", ["boarsmounted"] = "BoarMount",
        ["flameatronach"] = "AtronachFlame", ["frostatronach"] = "AtronachFrost",
        ["stormatronach"] = "AtronachStorm", ["foxes"] = "Fox", ["netches"] = "Netch",
        ["werewolves"] = "Werewolf", ["slaughterfishes"] = "Slaughterfish",
        ["benthiclurkers"] = "Lurker", ["benthiclurker"] = "Lurker",
    };

    static readonly HashSet<string> FemaleCreatures = new() { "Spriggan", "AtronachFlame", "Hagraven" };

    static readonly string[] KnownIdles =
    {
        "OCrAtronachFlameIdleFF", "OCrAtronachFlameIdleMF", "OCrAtronachFrostIdleFM",
        "OCrAtronachStormIdleFM", "OCrBearIdleFM", "OCrBoarIdleFM", "OCrBoarMountIdleFFM",
        "OCrBoarMountIdleFM", "OCrCanineIdleFFM", "OCrCanineIdleFM", "OCrCanineIdleFMM",
        "OCrCanineIdleFMMM", "OCrCanineIdleFMMMM", "OCrChaurusIdleFM",
        "OCrChaurusFlyerIdleFM", "OCrChaurusFlyerIdleFMM", "OCrChaurusReaperIdleFM",
        "OCrDeerIdleFM", "OCrDragonIdleFM", "OCrDragonPriestIdleFM", "OCrDraugrIdleFFM",
        "OCrDraugrIdleFM", "OCrDraugrIdleFMM", "OCrDraugrIdleFMMM", "OCrDraugrIdleFMMMM",
        "OCrDwarvenBallistaIdleFM", "OCrDwarvenCenturionIdleFM", "OCrDwarvenSphereIdleFM",
        "OCrDwarvenSpiderIdleFM", "OCrFalmerIdleFFM", "OCrFalmerIdleFM", "OCrFalmerIdleFMM",
        "OCrFalmerIdleFMMM", "OCrFalmerIdleFMMMM", "OCrFoxIdleFM", "OCrGargoyleIdleFFM",
        "OCrGargoyleIdleFM", "OCrGargoyleIdleFMM", "OCrGiantIdleFM", "OCrGoatIdleFM",
        "OCrHagravenIdleFF", "OCrHorkerIdleFM", "OCrHorseIdleFFM", "OCrHorseIdleFM",
        "OCrHorseIdleFMM", "OCrIceWraithIdleFM", "OCrLurkerIdleFM", "OCrMammothIdleFM",
        "OCrNetchIdleFM", "OCrRabbitIdleFM", "OCrRieklingIdleFFM", "OCrRieklingIdleFM",
        "OCrRieklingIdleFMM", "OCrRieklingIdleFMMM", "OCrRieklingIdleFMMMM",
        "OCrSabreCatIdleFM", "OCrScribIdleFM", "OCrScribIdleFMM", "OCrSeekerIdleFM",
        "OCrSkeeverIdleFFM", "OCrSkeeverIdleFM", "OCrSkeeverIdleFMM",
        "OCrSlaughterFishIdleFM", "OCrSpiderIdleFM", "OCrSpiderIdleFMM",
        "OCrSpiderGiantIdleFM", "OCrSpiderLargeIdleFM", "OCrSprigganIdleFF",
        "OcrSprigganIdleMF", "OCrTrollIdleFM", "OCrTrollIdleFMM", "OCrTrollIdleFMMMM",
        "OCrVampireLordIdleFM", "OCrWerewolfIdleFFM", "OCrWerewolfIdleFM",
        "OCrWerewolfIdleFMM", "OCrWerewolfIdleFMMM", "OCrWerewolfIdleFMMMM",
    };

    static readonly Dictionary<string, string> IconFamily = new()
    {
        ["AtronachFlame"] = "atronach", ["AtronachFrost"] = "atronach", ["AtronachStorm"] = "atronach",
        ["BoarMount"] = "boar", ["ChaurusFlyer"] = "chaurus", ["ChaurusReaper"] = "chaurus",
        ["DwarvenBallista"] = "dwarven", ["DwarvenCenturion"] = "dwarven",
        ["DwarvenSphere"] = "dwarven", ["DwarvenSpider"] = "dwarven",
        ["SpiderGiant"] = "spider", ["SpiderLarge"] = "spider", ["Dragonpriest"] = "draugr",
    };

    static readonly HashSet<string> OwnRaceIcon = new()
    {
        "AtronachFlame", "AtronachFrost", "AtronachStorm", "BoarMount", "ChaurusFlyer",
        "DwarvenBallista", "DwarvenCenturion", "DwarvenSphere", "DwarvenSpider", "Dragonpriest",
    };

    // Fallback SLAL actor number -> animation index when the SLAL json is unknown
    static readonly Dictionary<int, int> ActorIndex = new() { [1] = 1, [2] = 0, [3] = 2, [4] = 3, [5] = 4 };

    static readonly Regex EventRe = new(@"_A(\d+)_S(\d+)", RegexOptions.IgnoreCase);

    static int DefaultIndex(int n) => ActorIndex.TryGetValue(n, out var i) ? i : n - 1;

    // ------------------------------------------------------------ helpers

    public static string ReadText(string path)
    {
        var raw = File.ReadAllBytes(path);
        int skip = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF ? 3 : 0;
        try { return new UTF8Encoding(false, true).GetString(raw, skip, raw.Length - skip); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(raw); }
    }

    static IEnumerable<string> SplitLines(string text)
    {
        using var r = new StringReader(text);
        string line;
        while ((line = r.ReadLine()) != null) yield return line;
    }

    static void WriteText(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    static string Alnum(string s) => Regex.Replace(s ?? "", "[^A-Za-z0-9]", "");

    /// Python-like sort of paths (case-insensitive, part by part)
    static int ComparePaths(string a, string b)
    {
        var pa = a.ToLowerInvariant().Split('\\', '/');
        var pb = b.ToLowerInvariant().Split('\\', '/');
        for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            int c = string.CompareOrdinal(pa[i], pb[i]);
            if (c != 0) return c;
        }
        return pa.Length.CompareTo(pb.Length);
    }

    // JsonElement helpers (python: d.get(key) / str(...))
    static JsonElement? Get(JsonElement obj, string key) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(key, out var v) ? v : null;

    static string Str(JsonElement? e)
    {
        if (e == null) return "";
        var v = e.Value;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            _ => v.GetRawText(),
        };
    }

    static List<JsonElement> Arr(JsonElement? e) =>
        e is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray().ToList() : new List<JsonElement>();

    static string Or(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "";

    static JsonDocument ParseJson(string path) => JsonDocument.Parse(ReadText(path),
        new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

    // ------------------------------------------------------------ event numbering

    /// first creature -> 0, A1 -> 1, the rest -> 2, 3 ... in SLAL order
    /// F,C: A2->0 A1->1   F,F,C: A3->0 A1->1 A2->2   F,C,C: A2->0 A1->1 A3->2
    public static Dictionary<int, int> IndexMap(List<(int n, bool cr)> kinds)
    {
        var nums = kinds.Select(k => k.n).ToList();
        int? first = kinds.Where(k => k.cr).Select(k => (int?)k.n).FirstOrDefault();
        if (first == null) return nums.ToDictionary(n => n, DefaultIndex);
        var order = new List<int> { first.Value };
        if (first != 1 && nums.Contains(1)) order.Add(1);
        order.AddRange(nums.Where(n => n != first && n != 1));
        var map = new Dictionary<int, int>();
        for (int i = 0; i < order.Count; i++) map[order[i]] = i;
        return map;
    }

    public static Dictionary<string, Dictionary<int, int>> LoadIndexMaps(IEnumerable<string> jsonFiles)
    {
        var maps = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var js in jsonFiles)
        {
            JsonDocument doc;
            try { doc = ParseJson(js); } catch { continue; }
            using (doc)
            {
                foreach (var anim in Arr(Get(doc.RootElement, "animations")))
                {
                    var actors = Arr(Get(anim, "actors"));
                    var kinds = actors.Select((a, i) =>
                        (i + 1, Str(Get(a, "type")).ToLowerInvariant().StartsWith("creature"))).ToList();
                    var imap = IndexMap(kinds);
                    foreach (var a in actors)
                        foreach (var st in Arr(Get(a, "stages")))
                        {
                            var id = Str(Get(st, "id"));
                            var m = EventRe.Match(id);
                            if (m.Success) maps[id[..m.Index]] = imap;
                        }
                }
            }
        }
        return maps;
    }

    public static List<string> FindSlalJsons(string root)
    {
        var list = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Where(p =>
            {
                var dir = Path.GetDirectoryName(p);
                return Path.GetFileName(dir).Equals("json", StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(Path.GetDirectoryName(dir)).Equals("slanims", StringComparison.OrdinalIgnoreCase);
            }).ToList();
        list.Sort(ComparePaths);
        return list;
    }

    public static string RenameEvent(string name, Dictionary<string, Dictionary<int, int>> maps)
    {
        // half-converted names like X_A1_S2_1 -> drop the old trailing _1 too
        if (Regex.IsMatch(name, @"_A\d+_S\d+_\d+$", RegexOptions.IgnoreCase))
            name = Regex.Replace(name, @"_\d+$", "");
        var src = name;
        return EventRe.Replace(src, m =>
        {
            int n = int.Parse(m.Groups[1].Value);
            int idx = maps != null && maps.TryGetValue(src[..m.Index], out var imap) && imap.TryGetValue(n, out var v)
                ? v : DefaultIndex(n);
            return $"_S{m.Groups[2].Value}_{idx}";
        });
    }

    /// "Pack_A1_S3" -> ("Pack_S3", 3)
    static (string, int) StageBase(string stageId)
    {
        var m = EventRe.Match(stageId);
        if (!m.Success) return (stageId, 1);
        return (stageId[..m.Index] + "_S" + m.Groups[2].Value + stageId[(m.Index + m.Length)..],
                int.Parse(m.Groups[2].Value));
    }

    // ------------------------------------------------------------ FNIS lists

    static readonly HashSet<string> AnimTypes = new() { "b", "s", "+", "fu", "fuo", "o", "so", "a", "-" };
    static readonly Regex KeepOption = new(@"^(a|p|o|AVI?\w+)$", RegexOptions.IgnoreCase);

    /// FNIS line -> AnimlistTransitionTool line. kind = anim / comment / blank
    public static (string line, string kind) ConvertFnisLine(string line, Dictionary<string, Dictionary<int, int>> maps)
    {
        var stripped = line.Trim();
        if (stripped.Length == 0) return ("", "blank");
        if (stripped.StartsWith("'")) return (stripped, "comment");
        var parts = stripped.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!AnimTypes.Contains(parts[0].ToLowerInvariant()))
            // Version line, AnimVar, ';' / '#' comments -> must be a ' comment
            return ("' " + stripped.TrimStart(';', '#', '/', ' '), "comment");

        var rest = parts.Skip(1).ToList();
        var options = new List<string>();
        if (rest.Count > 0 && rest[0].StartsWith("-"))
        {
            options = rest[0][1..].Split(',').Where(o => o.Length > 0).ToList();
            rest = rest.Skip(1).ToList();
        }
        if (rest.Count < 2) return ("' (unparsed) " + stripped, "comment");
        string ev = RenameEvent(rest[0], maps), hkx = rest[1];
        var objects = rest.Skip(2).ToList();

        var kept = new List<string>();
        foreach (var o in options)
            if (KeepOption.IsMatch(o) && !kept.Any(k => k.Equals(o, StringComparison.OrdinalIgnoreCase)))
                kept.Add(o.Length == 1 ? o.ToLowerInvariant() : o);
        if (objects.Count > 0 && !kept.Contains("o")) kept.Add("o");
        if (objects.Count == 0) kept.Remove("o");
        // order like FNIS for Modders output: Tn, o, then the rest (stable)
        kept = kept.OrderBy(o => o != "o").ThenBy(o => o.StartsWith("AV", StringComparison.Ordinal)).ToList();
        var opt = string.Join(",", new[] { "Tn" }.Concat(kept));
        return (string.Join(" ", new[] { "b", "-" + opt, ev, hkx }.Concat(objects)), "anim");
    }

    public int ConvertFnisFile(string src, string dst, Dictionary<string, Dictionary<int, int>> maps)
    {
        var outLines = new List<string>();
        int anims = 0;
        foreach (var l in SplitLines(ReadText(src)))
        {
            var (nl, kind) = ConvertFnisLine(l, maps);
            if (kind == "anim" && nl.Contains('\''))
            {
                nl = "' (has quote, skipped) " + nl;
                kind = "comment";
            }
            if (kind == "blank" && (outLines.Count == 0 || outLines[^1] == "")) continue;
            outLines.Add(nl);
            if (kind == "anim") anims++;
        }
        while (outLines.Count > 0 && outLines[^1] == "") outLines.RemoveAt(outLines.Count - 1);
        WriteText(dst, string.Join(PyJson.NL, outLines) + PyJson.NL);
        bool character = src.Split('\\', '/').Any(p => p.Equals("character", StringComparison.OrdinalIgnoreCase));
        Log($"  [{(character ? "character" : "creature")}] {Path.GetFileName(src)}: {anims} animation line(s) -> b -Tn");
        return anims;
    }

    // ------------------------------------------------------------ FNIS for Modders

    static string BehaviorName(string listName) =>
        Regex.Replace(listName, @"_list\.txt$", "_Behavior.hkx", RegexOptions.IgnoreCase);

    /// Reads a file another process still has open (FNIS keeps its log open).
    static string ReadShared(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            return Encoding.Latin1.GetString(ms.ToArray());
        }
        catch { return ""; }
    }

    static void KillQuietly(Process p)
    {
        try { if (!p.HasExited) { p.Kill(); p.WaitForExit(5000); } } catch { }
    }

    /// hkxcmd convert with properly quoted paths. SE (AMD64) is chosen the same way FNIS does it.
    bool RunHkxcmd(string exeDir, string xml, string hkx)
    {
        var hkxcmd = Path.Combine(exeDir, "hkxcmd.exe");
        if (!File.Exists(hkxcmd)) return false;
        var users = Path.Combine(Path.GetDirectoryName(exeDir), "GenerateFNIS_for_Users");
        bool se = File.Exists(Path.Combine(users, "PatchListSE.txt"))
                  || File.Exists(Path.Combine(exeDir, "..", "..", "..", "SkyrimSE.exe"))
                  || !File.Exists(Path.Combine(users, "PatchList.txt"));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(hkx));
            var psi = new ProcessStartInfo(hkxcmd)
            {
                WorkingDirectory = exeDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in new[] { "convert", se ? "-v:AMD64" : "-v:WIN32", xml, hkx }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            p.StandardOutput.ReadToEndAsync();
            p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(120_000)) { KillQuietly(p); return false; }
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    /// Runs GenerateFNISforModders.exe on one list (no GUI).
    /// Result: [list]/../../behaviors/FNIS_[mod]_Behavior.hkx
    public bool RunFnis(string listPath)
    {
        listPath = Path.GetFullPath(listPath);
        var exe = Path.GetFullPath(FnisExe);
        if (!File.Exists(exe))
        {
            Log($"  ERROR: FNIS for Modders not found: {exe}");
            return false;
        }
        var exeDir = Path.GetDirectoryName(exe);
        var logFile = Path.Combine(exeDir, "temporary_logs", "GenerateFNIS_LogFile.txt");
        var listDir = Path.GetDirectoryName(listPath);
        var actorDir = Path.GetDirectoryName(Path.GetDirectoryName(listDir));
        var hkx = Path.Combine(actorDir, "behaviors", BehaviorName(Path.GetFileName(listPath)));
        // character lists: FNIS always writes to its OWN folder [FNIS]\meshes\actors\character\behaviors
        var fnisRoot = Path.GetDirectoryName(Path.GetDirectoryName(exeDir));
        var fnisStd = Path.Combine(fnisRoot, "meshes", "actors", "character", "behaviors", Path.GetFileName(hkx));
        try { if (File.Exists(logFile)) File.Delete(logFile); }
        catch (IOException)
        {
            Log("  ERROR: FNIS for Modders is already open - close it and try again.");
            return false;
        }
        var started = DateTime.Now.AddSeconds(-2);
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                WorkingDirectory = exeDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add(listPath);
            using var p = Process.Start(psi);
            // FNIS shows a modal error box on ERROR(...) and never exits by itself:
            // watch its log and close it as soon as an error is written.
            var sw = Stopwatch.StartNew();
            while (!p.WaitForExit(300))
            {
                if (Regex.IsMatch(ReadShared(logFile), @"ERROR\(\d+\)"))
                {
                    Thread.Sleep(300);
                    KillQuietly(p);
                    break;
                }
                if (sw.Elapsed.TotalSeconds > 300)
                {
                    KillQuietly(p);
                    Log($"  ERROR: FNIS timed out on {Path.GetFileName(listPath)}");
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            Log($"  ERROR: cannot start FNIS: {e.Message}");
            return false;
        }
        var text = ReadShared(logFile);
        bool Fresh(string f) => File.Exists(f) && File.GetLastWriteTime(f) >= started;
        string Shown() => Path.GetDirectoryName(actorDir) is string up ? Path.GetRelativePath(up, hkx) : Path.GetFileName(hkx);
        if (text.Contains("successfully created") && (Fresh(hkx) || Fresh(fnisStd)))
        {
            if (!Fresh(hkx))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(hkx));
                File.Copy(fnisStd, hkx, true);
            }
            Log($"  OK  {Shown()}");
            return true;
        }
        // ERROR(2012): FNIS passes the xml to hkxcmd WITHOUT quotes, so a mod name with
        // spaces ("Babo Creature") breaks the conversion. The xml is fine -> convert it ourselves.
        var xml = Path.Combine(exeDir, "temporary_logs", Path.ChangeExtension(Path.GetFileName(hkx), ".xml"));
        if (text.Contains("ERROR(2012)") && Fresh(xml))
        {
            if (RunHkxcmd(exeDir, xml, hkx) && Fresh(hkx))
            {
                Log($"  OK  {Shown()}  (FNIS ERROR 2012 fixed: converted with hkxcmd directly)");
                return true;
            }
        }
        var errors = SplitLines(text).Select(l => l.Trim()).Where(l => l.ToUpperInvariant().Contains("ERROR")).ToList();
        if (errors.Any(e => e.Contains("being used by another process")))
            errors = new() { "FNIS for Modders is already open - close it and try again." };
        Log($"  FAILED {Path.GetFileName(listPath)}: " +
            (errors.Count > 0 ? string.Join(" | ", errors.TakeLast(3)) : "see " + logFile));
        return false;
    }

    /// Convert one FNIS_*_List.txt.
    /// Inside a mod ([actor]\animations\[Mod]\) -> in place (.bak kept) + behavior hkx.
    /// Anywhere else -> [txt folder]\OStim\[same name], no behavior.
    public bool ConvertTxt(string txt)
    {
        txt = Path.GetFullPath(txt);
        if (!File.Exists(txt))
        {
            Log($"ERROR: file not found: {txt}");
            return false;
        }
        var modDir = Path.GetDirectoryName(txt);
        var animDir = Path.GetDirectoryName(modDir);
        bool inMod = animDir != null && Path.GetFileName(animDir).Equals("animations", StringComparison.OrdinalIgnoreCase);
        // event numbering comes from the pack's SLAnims json ([pack]\meshes\.. -> [pack]\SLAnims)
        string root = null;
        for (var d = modDir; d != null; d = Path.GetDirectoryName(d))
            if (Path.GetFileName(d).Equals("meshes", StringComparison.OrdinalIgnoreCase)) { root = Path.GetDirectoryName(d); break; }
        var jsons = root != null ? FindSlalJsons(root) : new List<string>();
        var maps = LoadIndexMaps(jsons);
        if (jsons.Count == 0)
            Log("  (no SLAnims json found next to meshes - using default A2->0, A1->1 numbering)");

        if (!(GenBehaviors && inMod))
        {
            var dst = Path.Combine(modDir, "OStim", Path.GetFileName(txt));
            ConvertFnisFile(txt, dst, maps);
            Log($"Saved: {dst}");
            if (GenBehaviors) Log("  (not inside <actor>\\animations\\<Mod>\\ - behavior hkx not generated)");
            return true;
        }
        var hkxPath = Path.Combine(Path.GetDirectoryName(animDir), "behaviors", BehaviorName(Path.GetFileName(txt)));
        foreach (var f in new[] { txt, hkxPath })
        {
            var bak = f + ".bak";
            if (File.Exists(f) && !File.Exists(bak))
            {
                File.Copy(f, bak);
                Log($"  backup: {Path.GetFileName(bak)}");
            }
        }
        ConvertFnisFile(txt, txt, maps);
        return RunFnis(txt);
    }

    // ------------------------------------------------------------ SLAL json -> OStim scenes

    record SlalActor(int N, bool IsCr, char Sex, string RaceKey);

    class Entry
    {
        public JsonObject Obj;
        public bool Creature;
    }

    public static string ResolveRace(string raceKey)
    {
        var k = Alnum(raceKey).ToLowerInvariant();
        if (k.Length == 0) return null;
        if (RaceAliases.TryGetValue(k, out var a)) return a;
        var lower = OcrRaces.ToDictionary(r => r.ToLowerInvariant());
        foreach (var cand in new[] { k, k.EndsWith("es") ? k[..^2] : null, k.EndsWith("s") ? k[..^1] : null })
            if (cand != null && lower.TryGetValue(cand, out var r)) return r;
        return null;
    }

    static string FindIdle(string race, string code)
    {
        var want = $"ocr{race}idle{code}".ToLowerInvariant();
        return KnownIdles.FirstOrDefault(i => i.ToLowerInvariant() == want);
    }

    static string Family(string race) => IconFamily.TryGetValue(race, out var f) ? f : race.ToLowerInvariant();

    static string RaceIcon(string race)
    {
        var fam = Family(race);
        return $"OCreatures/{fam}/{(OwnRaceIcon.Contains(race) ? race.ToLowerInvariant() : fam)}";
    }

    static string AnimIcon(string race, List<string> tags)
    {
        var fam = Family(race);
        bool Any(params string[] t) => tags.Any(t.Contains);
        string suffix = Any("oral", "blowjob") ? "oral"
            : Any("anal", "doggy") ? "anal"
            : Any("foot", "feet", "footjob") ? "foot"
            : Any("hand", "fist", "handjob") ? "hand"
            : Any("boobjob", "titfuck", "paizuri") ? "pai"
            : "vag";
        return $"OCreatures/{fam}/{fam}{suffix}";
    }

    static List<string> ParseTags(JsonElement? raw)
    {
        IEnumerable<string> items = raw is { ValueKind: JsonValueKind.Array } a
            ? a.EnumerateArray().Select(x => Str(x))
            : Str(raw).Split(',');
        return items.Select(t => t.Trim()).Where(t => t.Length > 0).Select(t => t.ToLowerInvariant()).ToList();
    }

    static List<SlalActor> SlalActors(JsonElement anim)
    {
        var list = new List<SlalActor>();
        int n = 0;
        foreach (var actor in Arr(Get(anim, "actors")))
        {
            n++;
            var t = Str(Get(actor, "type")).ToLowerInvariant();
            list.Add(new SlalActor(n, t.StartsWith("creature"), t.Contains("female") ? 'f' : 'm',
                Or(Str(Get(actor, "race")), Str(Get(anim, "creature_race")))));
        }
        return list;
    }

    static string SceneCode(List<SlalActor> actors, string race)
    {
        var hum = new string(actors.Where(a => !a.IsCr).Select(a => a.Sex).OrderBy(c => c).ToArray());
        var cre = new string(actors.Where(a => a.IsCr).Select(a => FemaleCreatures.Contains(race) ? 'f' : a.Sex).OrderBy(c => c).ToArray());
        return hum + cre;
    }

    static List<Entry> SceneActorEntries(List<SlalActor> actors, string race)
    {
        var ordered = actors.Where(a => a.IsCr)
            .Concat(actors.Where(a => !a.IsCr && a.Sex == 'm'))
            .Concat(actors.Where(a => !a.IsCr && a.Sex == 'f')).ToList();
        var imap = IndexMap(actors.Select(a => (a.N, a.IsCr)).ToList());
        bool needIndex = ordered.Count > 2 || ordered.Select((a, i) => imap[a.N] != i).Any(x => x);
        var entries = new List<Entry>();
        foreach (var a in ordered)
        {
            var e = new JsonObject();
            char sex = a.Sex;
            if (a.IsCr)
            {
                e["type"] = "cr" + race.ToLowerInvariant();
                if (FemaleCreatures.Contains(race)) sex = 'f';
            }
            e["intendedSex"] = sex == 'f' ? "female" : "male";
            if (needIndex) e["animationIndex"] = imap[a.N];
            if (sex == 'm') e["requirements"] = new JsonArray("penis");
            e["tags"] = new JsonArray("standing");
            entries.Add(new Entry { Obj = e, Creature = a.IsCr });
        }
        return entries;
    }

    static JsonArray SceneActions(List<string> tags, List<Entry> entries)
    {
        var acts = new JsonArray();
        int c = entries.FindIndex(e => e.Creature), h = entries.FindIndex(e => !e.Creature);
        if (c < 0 || h < 0) return acts;
        bool cMale = entries[c].Obj["intendedSex"].GetValue<string>() == "male";
        var have = new HashSet<string>();
        void Add(string kind, int actor, int target)
        {
            if (have.Add(kind)) acts.Add(new JsonObject { ["type"] = kind, ["actor"] = actor, ["target"] = target });
        }
        foreach (var t in tags)
        {
            if (t is "oral" or "blowjob" or "deepthroat" && cMale) Add("blowjob", h, c);
            else if (t is "boobjob" or "titfuck" or "paizuri") Add("boobjob", h, c);
            else if (t == "handjob") Add("handjob", h, c);
            else if (t == "footjob") Add("footjob", h, c);
            else if (t == "anal" && cMale) Add("analsex", c, h);
            else if (t == "vaginal" && cMale) Add("vaginalsex", c, h);
            else if (t == "cunnilingus") Add("cunnilingus", c, h);
        }
        if (acts.Count == 0 && cMale) Add("vaginalsex", c, h);
        return acts;
    }

    static JsonArray CleanActors(List<Entry> entries) => new(entries.Select(e => e.Obj.DeepClone()).ToArray());

    static JsonArray StrArray(IEnumerable<string> items) => new(items.Select(s => (JsonNode)s).ToArray());

    static void WriteJson(string path, JsonNode data) => WriteText(path, PyJson.Dump(data) + PyJson.NL);

    public class SceneState
    {
        public HashSet<string> Hubs = new();
        public HashSet<string> Ids = new();
    }

    public int BuildScenes(string slalJson, string prefix, string outRoot, SceneState state)
    {
        using var doc = ParseJson(slalJson);
        var data = doc.RootElement;
        if (string.IsNullOrEmpty(prefix))
            prefix = Or(Alnum(Str(Get(data, "name")).Split('_')[0]), Alnum(Path.GetFileNameWithoutExtension(slalJson)), "Custom");
        var scenesRoot = Path.Combine(outRoot, "SKSE", "Plugins", "OStim", "scenes", "OCreatures");
        int count = 0;
        foreach (var anim in Arr(Get(data, "animations")))
        {
            var actors = SlalActors(anim);
            var firstActor = Arr(Get(anim, "actors")).FirstOrDefault();
            var stages = firstActor.ValueKind == JsonValueKind.Object ? Arr(Get(firstActor, "stages")) : new List<JsonElement>();
            if (actors.Count == 0 || stages.Count == 0) continue;
            var animName = Str(Get(anim, "name"));
            var cr = actors.Where(a => a.IsCr).ToList();
            if (cr.Count == 0)
            {
                Log($"  skip (no creature): {animName}");
                continue;
            }
            var race = ResolveRace(Or(cr[0].RaceKey, Str(Get(anim, "creature_race"))));
            if (race == null)
            {
                Log($"  skip (race '{cr[0].RaceKey}' not in OCreatures): {animName}");
                continue;
            }
            var code = SceneCode(actors, race);
            var tags = ParseTags(Get(anim, "tags"));
            var entries = SceneActorEntries(actors, race);
            var actions = SceneActions(tags, entries);
            var groupDir = Path.Combine(scenesRoot, $"OCr{race}", code, $"{prefix}{race}");
            var hub = $"{prefix}{race}Hub{code.ToUpperInvariant()}";
            var modpack = $"{prefix}Creatures";
            var sceneTags = new[] { prefix.ToLowerInvariant(), "creature", race.ToLowerInvariant() };
            var bases = stages.Select(s => StageBase(Str(Get(s, "id"))).Item1).ToList();

            // ---- hub (one per prefix / race / code)
            if (state.Hubs.Add(hub))
            {
                var idle = FindIdle(race, code);
                if (idle == null)
                {
                    idle = $"OCr{race}Idle{code.ToUpperInvariant()}";
                    Log($"  WARNING: OCreatures has no {idle} - hub {hub} won't show in the menu");
                }
                WriteJson(Path.Combine(groupDir, hub + ".json"), new JsonObject
                {
                    ["name"] = hub,
                    ["modpack"] = modpack,
                    ["length"] = 2,
                    ["navigations"] = new JsonArray(
                        new JsonObject { ["origin"] = idle, ["description"] = $"{prefix} {race} Hub", ["icon"] = RaceIcon(race) },
                        new JsonObject { ["destination"] = idle, ["description"] = "Exit Hub", ["icon"] = "OStim/symbols/return", ["priority"] = -1000 }),
                    ["speeds"] = new JsonArray(new JsonObject { ["animation"] = bases[0] }),
                    ["actors"] = CleanActors(entries),
                    ["tags"] = StrArray(new[] { "idle", "intro", "creature", race.ToLowerInvariant() }),
                });
                Log($"  hub:   OCr{race}/{code}/{prefix}{race}/{hub}.json");
            }

            // ---- animation stages
            var animId = Alnum(Regex.Replace(Or(animName, Str(Get(anim, "id")), "Anim"), "[()]", ""));
            if (!animId.ToLowerInvariant().StartsWith(prefix.ToLowerInvariant())) animId = prefix + animId;
            var baseId = animId;
            for (int k = 2; state.Ids.Contains(animId.ToLowerInvariant()); k++) animId = baseId + k;
            state.Ids.Add(animId.ToLowerInvariant());
            var title = Regex.Replace(Regex.Replace(Or(animName, animId), "[()]", ""), @"\s+", " ").Trim();
            int total = stages.Count;
            string Sid(int k) => $"{animId}-{k}";
            for (int n = 1; n <= total; n++)
            {
                bool climax = total >= 2 && n == total;
                bool beforeClimax = total >= 2 && n == total - 1;
                var scene = new JsonObject { ["name"] = title, ["modpack"] = modpack, ["length"] = climax ? 8 : 2 };
                if (climax)
                    scene["destination"] = Sid(n - 1);
                else
                {
                    var nav = new JsonArray();
                    if (n == 1)
                        nav.Add(new JsonObject { ["origin"] = hub, ["description"] = title, ["icon"] = AnimIcon(race, tags) });
                    if (n < total)
                        nav.Add(new JsonObject
                        {
                            ["destination"] = Sid(n + 1),
                            ["description"] = beforeClimax ? "Next (Climax)" : "Next",
                            ["icon"] = beforeClimax ? "OStim/symbols/climax" : "OStim/symbols/next",
                        });
                    if (n > 1)
                        nav.Add(new JsonObject { ["destination"] = Sid(n - 1), ["description"] = "Return", ["icon"] = "OStim/symbols/return" });
                    nav.Add(new JsonObject { ["destination"] = hub, ["description"] = "Return to Hub", ["icon"] = RaceIcon(race) });
                    scene["navigations"] = nav;
                }
                var stageActors = CleanActors(entries);
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = stageActors[i].AsObject();
                    if (beforeClimax) e["autoTransitions"] = new JsonObject { ["climax"] = Sid(n + 1) };
                    if (climax && entries[i].Creature) e["tags"].AsArray().Add("climaxing");
                }
                scene["speeds"] = new JsonArray(new JsonObject { ["animation"] = bases[n - 1] });
                scene["actors"] = stageActors;
                scene["actions"] = actions.DeepClone();
                scene["tags"] = StrArray(sceneTags);
                WriteJson(Path.Combine(groupDir, animId, Sid(n) + ".json"), scene);
            }
            count++;
            Log($"  scene: OCr{race}/{code}/{prefix}{race}/{animId} ({total} stages)");
        }
        return count;
    }

    // ------------------------------------------------------------ main job

    public bool Convert(string packDir, string outDir)
    {
        packDir = Path.GetFullPath(packDir.TrimEnd('\\', '/'));
        outDir = Path.GetFullPath(outDir.TrimEnd('\\', '/'));
        if (!Directory.Exists(packDir))
        {
            Log($"ERROR: folder not found: {packDir}");
            return false;
        }
        if (string.Equals(packDir, outDir, StringComparison.OrdinalIgnoreCase))
        {
            Log("ERROR: output folder must be different from the pack folder");
            return false;
        }
        if (outDir.StartsWith(packDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            Log("ERROR: output folder must not be inside the pack folder");
            return false;
        }
        // selecting the meshes folder itself still gives [out]\meshes\...
        var outBase = Path.GetFileName(packDir).Equals("meshes", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(outDir, "meshes") : outDir;
        Log($"Pack:   {packDir}");
        Log($"Output: {outDir}");

        // 1+2: copy the whole pack, converting FNIS lists on the way
        Log("\n[Copy pack + FNIS lists]");
        var jsons = FindSlalJsons(packDir);
        var maps = LoadIndexMaps(jsons);
        var lists = new List<string>();
        int copied = 0;
        foreach (var src in Directory.EnumerateFiles(packDir, "*", SearchOption.AllDirectories))
        {
            var dst = Path.Combine(outBase, Path.GetRelativePath(packDir, src));
            var name = Path.GetFileName(src).ToLowerInvariant();
            if (name.StartsWith("fnis_") && name.EndsWith("_list.txt"))
            {
                ConvertFnisFile(src, dst, maps);
                lists.Add(dst);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, true);
                copied++;
            }
        }
        Log($"  copied {copied} file(s)");
        if (lists.Count == 0) Log("  (no FNIS_*_List.txt found)");

        // 3: FNIS for Modders
        int behaviors = 0;
        if (GenBehaviors && lists.Count > 0)
        {
            Log("\n[FNIS behaviors]");
            foreach (var l in lists)
                if (RunFnis(l)) behaviors++;
        }

        // 4: SLAL json -> OStim scenes
        Log("\n[OStim scenes]");
        int scenes = 0;
        var state = new SceneState();
        foreach (var js in jsons)
        {
            try { scenes += BuildScenes(js, (Prefix ?? "").Trim(), outDir, state); }
            catch (Exception e) { Log($"  FAILED {Path.GetFileName(js)}: {e.Message}"); }
        }
        if (scenes == 0) Log("  (no SLAnims/json files found - only FNIS lists converted)");

        var summary = $"\nDone: {copied} file(s) copied, {lists.Count} FNIS list(s), {scenes} animation(s) / {state.Hubs.Count} hub(s)";
        if (GenBehaviors && lists.Count > 0) summary += $", {behaviors}/{lists.Count} behavior hkx";
        Log(summary + ".");
        bool failed = GenBehaviors && lists.Count > 0 && behaviors < lists.Count;
        if (failed) Log("Some behaviors FAILED - see the messages above.");
        Log("Next: install the output folder as a mod, then run Nemesis/Pandora.");
        return !failed;
    }
}
