using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MelonLoader;

[assembly: MelonInfo(typeof(MuseDashTrak.TrakMod), "MuseDashTrak", "0.1.0", "dsh")]
[assembly: MelonGame(null, null)]

namespace MuseDashTrak
{
    // ---------------------------------------------------------------------
    // Muse Dash is an IL2CPP game, so GameAssembly.dll exports the whole
    // il2cpp_* API. Everything below is resolved *by name* at runtime, which
    // avoids referencing version-specific generated assemblies and keeps the
    // mod alive across game updates.
    // ---------------------------------------------------------------------
    internal static class Api
    {
        private const string Dll = "GameAssembly.dll";

        [DllImport(Dll)] internal static extern IntPtr il2cpp_domain_get();
        [DllImport(Dll)] internal static extern IntPtr il2cpp_domain_get_assemblies(IntPtr domain, ref IntPtr size);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_assembly_get_image(IntPtr assembly);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_from_name(IntPtr image, string ns, string name);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_get_parent(IntPtr klass);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_get_name(IntPtr klass);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_get_namespace(IntPtr klass);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_get_field_from_name(IntPtr klass, string name);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_get_method_from_name(IntPtr klass, string name, int argsCount);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_get_fields(IntPtr klass, ref IntPtr iter);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_field_get_name(IntPtr field);
        [DllImport(Dll)] internal static extern int il2cpp_field_get_offset(IntPtr field);
        [DllImport(Dll)] internal static extern void il2cpp_field_get_value(IntPtr obj, IntPtr field, IntPtr buffer);
        [DllImport(Dll)] internal static extern void il2cpp_field_static_get_value(IntPtr field, IntPtr buffer);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_runtime_invoke(IntPtr method, IntPtr obj, IntPtr[] parameters, out IntPtr exception);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_object_unbox(IntPtr obj);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_object_get_class(IntPtr obj);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_string_chars(IntPtr str);
        [DllImport(Dll)] internal static extern int il2cpp_string_length(IntPtr str);
        [DllImport(Dll)] internal static extern void il2cpp_runtime_class_init(IntPtr klass);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_get_methods(IntPtr klass, ref IntPtr iter);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_class_get_type(IntPtr klass);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_type_get_object(IntPtr type);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_method_get_name(IntPtr method);
        [DllImport(Dll)] internal static extern uint il2cpp_image_get_class_count(IntPtr image);
        [DllImport(Dll)] internal static extern IntPtr il2cpp_image_get_class(IntPtr image, int index);

        private static readonly Dictionary<string, IntPtr> ClassCache = new Dictionary<string, IntPtr>();
        private static readonly List<IntPtr> Images = new List<IntPtr>();

        internal static void LoadImages()
        {
            if (Images.Count > 0)
                return;
            IntPtr domain = il2cpp_domain_get();
            if (domain == IntPtr.Zero)
                return;
            IntPtr count = IntPtr.Zero;
            IntPtr arr = il2cpp_domain_get_assemblies(domain, ref count);
            if (arr == IntPtr.Zero)
                return;
            long n = count.ToInt64();
            for (long i = 0; i < n; i++)
            {
                IntPtr assembly = Marshal.ReadIntPtr(arr, (int)i * IntPtr.Size);
                if (assembly == IntPtr.Zero)
                    continue;
                IntPtr image = il2cpp_assembly_get_image(assembly);
                if (image != IntPtr.Zero)
                    Images.Add(image);
            }
        }

        internal static IntPtr FindClass(string ns, string name)
        {
            string key = ns + "." + name;
            IntPtr cached;
            if (ClassCache.TryGetValue(key, out cached))
                return cached;
            LoadImages();
            foreach (IntPtr image in Images)
            {
                IntPtr klass = il2cpp_class_from_name(image, ns, name);
                if (klass != IntPtr.Zero)
                {
                    ClassCache[key] = klass;
                    return klass;
                }
            }
            // Namespace changed in a game update? Fall back to a name-only scan.
            foreach (IntPtr image in Images)
            {
                IntPtr klass = il2cpp_class_from_name(image, "", name);
                if (klass != IntPtr.Zero)
                {
                    ClassCache[key] = klass;
                    return klass;
                }
            }
            return IntPtr.Zero;
        }

        internal static IntPtr FindField(IntPtr klass, string name)
        {
            IntPtr cur = klass;
            while (cur != IntPtr.Zero)
            {
                IntPtr f = il2cpp_class_get_field_from_name(cur, name);
                if (f != IntPtr.Zero)
                    return f;
                cur = il2cpp_class_get_parent(cur);
            }
            return IntPtr.Zero;
        }

        internal static IntPtr FindMethod(IntPtr klass, string name, int args)
        {
            IntPtr cur = klass;
            while (cur != IntPtr.Zero)
            {
                IntPtr m = il2cpp_class_get_method_from_name(cur, name, args);
                if (m != IntPtr.Zero)
                    return m;
                cur = il2cpp_class_get_parent(cur);
            }
            return IntPtr.Zero;
        }

        internal static string ClassName(IntPtr klass)
        {
            if (klass == IntPtr.Zero)
                return "<null>";
            string ns = Marshal.PtrToStringAnsi(il2cpp_class_get_namespace(klass));
            string n = Marshal.PtrToStringAnsi(il2cpp_class_get_name(klass));
            return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
        }

        private static IntPtr GetStaticObject(IntPtr klass, string field)
        {
            IntPtr f = FindField(klass, field);
            if (f == IntPtr.Zero)
                return IntPtr.Zero;
            IntPtr buf = Marshal.AllocHGlobal(16);
            try
            {
                try { il2cpp_runtime_class_init(klass); } catch { }
                il2cpp_field_static_get_value(f, buf);
                return Marshal.ReadIntPtr(buf);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        // Singleton accessors are properties in some builds and plain static
        // fields in others (and the field may be m_Instance/s_Instance), so
        // try every plausible spelling.
        internal static IntPtr Singleton(IntPtr klass, string name)
        {
            if (klass == IntPtr.Zero)
                return IntPtr.Zero;
            string[] names = new[] { name, "Instance", "m_Instance", "s_Instance", "m_instance", "s_instance" };
            foreach (string n in names)
            {
                IntPtr getter = FindMethod(klass, "get_" + n, 0);
                if (getter != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr exc;
                        IntPtr obj = il2cpp_runtime_invoke(getter, IntPtr.Zero, null, out exc);
                        if (obj != IntPtr.Zero)
                            return obj;
                    }
                    catch { }
                }
                IntPtr fromField = GetStaticObject(klass, n);
                if (fromField != IntPtr.Zero)
                    return fromField;
            }
            return IntPtr.Zero;
        }

        internal static IntPtr MemberObject(IntPtr obj, IntPtr klass, string name)
        {
            if (obj == IntPtr.Zero)
                return IntPtr.Zero;
            IntPtr f = FindField(klass, name);
            if (f != IntPtr.Zero)
            {
                IntPtr buf = Marshal.AllocHGlobal(16);
                try
                {
                    il2cpp_field_get_value(obj, f, buf);
                    return Marshal.ReadIntPtr(buf);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            IntPtr getter = FindMethod(klass, "get_" + name, 0);
            if (getter != IntPtr.Zero)
            {
                try
                {
                    IntPtr exc;
                    return il2cpp_runtime_invoke(getter, obj, null, out exc);
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        internal static int MemberInt(IntPtr obj, IntPtr klass, string name, int fallback)
        {
            if (obj == IntPtr.Zero)
                return fallback;
            IntPtr f = FindField(klass, name);
            if (f != IntPtr.Zero)
            {
                IntPtr buf = Marshal.AllocHGlobal(16);
                try
                {
                    il2cpp_field_get_value(obj, f, buf);
                    return Marshal.ReadInt32(buf);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            return InvokeInt(obj, klass, "get_" + name, fallback);
        }

        internal static int InvokeInt(IntPtr obj, IntPtr klass, string method, int fallback)
        {
            IntPtr m = FindMethod(klass, method, 0);
            if (m == IntPtr.Zero)
                return fallback;
            try
            {
                IntPtr exc;
                IntPtr boxed = il2cpp_runtime_invoke(m, obj, null, out exc);
                if (boxed == IntPtr.Zero)
                    return fallback;
                return Marshal.ReadInt32(il2cpp_object_unbox(boxed));
            }
            catch { return fallback; }
        }

        internal static string ReadString(IntPtr str)
        {
            if (str == IntPtr.Zero)
                return string.Empty;
            try
            {
                int len = il2cpp_string_length(str);
                if (len <= 0 || len > 4096)
                    return string.Empty;
                return Marshal.PtrToStringUni(il2cpp_string_chars(str), len);
            }
            catch { return string.Empty; }
        }

        internal static string MemberString(IntPtr obj, IntPtr klass, string name)
        {
            if (obj == IntPtr.Zero)
                return string.Empty;
            IntPtr f = FindField(klass, name);
            if (f != IntPtr.Zero)
            {
                IntPtr buf = Marshal.AllocHGlobal(16);
                try
                {
                    il2cpp_field_get_value(obj, f, buf);
                    return ReadString(Marshal.ReadIntPtr(buf));
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            IntPtr getter = FindMethod(klass, "get_" + name, 0);
            if (getter != IntPtr.Zero)
            {
                try
                {
                    IntPtr exc;
                    return ReadString(il2cpp_runtime_invoke(getter, obj, null, out exc));
                }
                catch { }
            }
            return string.Empty;
        }

        internal static float InvokeFloat(IntPtr obj, IntPtr klass, string method, float fallback)
        {
            IntPtr m = FindMethod(klass, method, 0);
            if (m == IntPtr.Zero)
                return fallback;
            try
            {
                IntPtr exc;
                IntPtr boxed = il2cpp_runtime_invoke(m, obj, null, out exc);
                if (boxed == IntPtr.Zero)
                    return fallback;
                return Marshal.PtrToStructure<float>(il2cpp_object_unbox(boxed));
            }
            catch { return fallback; }
        }

        internal static string InvokeIntString(IntPtr obj, IntPtr klass, string method, int arg)
        {
            IntPtr m = FindMethod(klass, method, 1);
            if (m == IntPtr.Zero)
                return string.Empty;
            IntPtr argBuf = Marshal.AllocHGlobal(8);
            try
            {
                Marshal.WriteInt32(argBuf, arg);
                IntPtr exc;
                IntPtr res = il2cpp_runtime_invoke(m, obj, new[] { argBuf }, out exc);
                return ReadString(res);
            }
            catch { return string.Empty; }
            finally { Marshal.FreeHGlobal(argBuf); }
        }

        // Last-resort instance lookup for components that are not exposed via a
        // static singleton: ask UnityEngine.Object.FindObjectOfType(Type).
        internal static IntPtr FindLiveObject(IntPtr klass)
        {
            if (klass == IntPtr.Zero)
                return IntPtr.Zero;
            try
            {
                try { il2cpp_runtime_class_init(klass); } catch { }
                IntPtr typeObj = il2cpp_type_get_object(il2cpp_class_get_type(klass));
                if (typeObj == IntPtr.Zero)
                    return IntPtr.Zero;
                IntPtr exc;

                IntPtr objClass = FindClass("UnityEngine", "Object");
                if (objClass != IntPtr.Zero)
                {
                    IntPtr m = FindMethod(objClass, "FindObjectOfType", 1);
                    if (m != IntPtr.Zero)
                    {
                        IntPtr found = il2cpp_runtime_invoke(m, IntPtr.Zero, new[] { typeObj }, out exc);
                        if (found != IntPtr.Zero)
                            return found;
                    }
                }

                // Fallback: Resources.FindObjectsOfTypeAll(Type) returns an array.
                IntPtr resClass = FindClass("UnityEngine", "Resources");
                if (resClass != IntPtr.Zero)
                {
                    IntPtr m2 = FindMethod(resClass, "FindObjectsOfTypeAll", 1);
                    if (m2 != IntPtr.Zero)
                    {
                        IntPtr arr = il2cpp_runtime_invoke(m2, IntPtr.Zero, new[] { typeObj }, out exc);
                        if (arr != IntPtr.Zero)
                        {
                            int len = Marshal.ReadInt32(arr, 0x18);
                            if (len > 0)
                            {
                                IntPtr first = Marshal.ReadIntPtr(arr, 0x20);
                                if (first != IntPtr.Zero)
                                    return first;
                            }
                        }
                    }
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        internal static string Hierarchy(IntPtr klass)
        {
            var sb = new StringBuilder();
            IntPtr cur = klass;
            int depth = 0;
            while (cur != IntPtr.Zero && depth++ < 8)
            {
                sb.Append(ClassName(cur)).Append(" <- ");
                cur = il2cpp_class_get_parent(cur);
            }
            return sb.ToString();
        }

        internal static void DumpMethods(IntPtr klass, StringBuilder sb)
        {
            if (klass == IntPtr.Zero) return;
            sb.AppendLine("methods " + ClassName(klass));
            IntPtr iter = IntPtr.Zero;
            while (true)
            {
                IntPtr m = il2cpp_class_get_methods(klass, ref iter);
                if (m == IntPtr.Zero)
                    break;
                string n = Marshal.PtrToStringAnsi(il2cpp_method_get_name(m));
                if (string.IsNullOrEmpty(n))
                    continue;
                if (n.StartsWith("get_") || n == "GetAccuracy" ||
                    n.IndexOf("Instance", StringComparison.OrdinalIgnoreCase) >= 0)
                    sb.AppendLine("  " + n);
            }
        }

        internal static void DumpMembers(IntPtr klass, StringBuilder sb)
        {
            if (klass == IntPtr.Zero) { sb.AppendLine("<null class>"); return; }
            sb.AppendLine("class " + ClassName(klass));
            IntPtr iter = IntPtr.Zero;
            while (true)
            {
                IntPtr f = il2cpp_class_get_fields(klass, ref iter);
                if (f == IntPtr.Zero)
                    break;
                string n = Marshal.PtrToStringAnsi(il2cpp_field_get_name(f));
                sb.AppendLine("  field " + n + " @0x" + il2cpp_field_get_offset(f).ToString("X"));
            }
        }

        // Only used when the expected classes could not be found: dumps every
        // class whose name looks relevant, so the user can send us the file.
        internal static void DumpCandidateClasses(StringBuilder sb)
        {
            LoadImages();
            sb.AppendLine("images: " + Images.Count);
            foreach (IntPtr image in Images)
            {
                uint count = 0;
                try { count = il2cpp_image_get_class_count(image); } catch { continue; }
                for (int i = 0; i < (int)count; i++)
                {
                    IntPtr klass = il2cpp_image_get_class(image, i);
                    if (klass == IntPtr.Zero)
                        continue;
                    string name = ClassName(klass);
                    if (name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("MusicInfo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("TaskStage", StringComparison.OrdinalIgnoreCase) >= 0)
                        sb.AppendLine(name);
                }
            }
        }
    }

    public class TrakMod : MelonMod
    {
        private const double PollInterval = 0.5;

        private IntPtr _clsDb, _clsMusicInfo, _clsStage, _clsTask, _clsRole;
        private bool _resolved;
        private bool _dumpDone;
        private int _resolveAttempts;
        private DateTime _lastPoll = DateTime.MinValue;
        private DateTime _battleStart = DateTime.MinValue;
        private DateTime _lastPlaying = DateTime.MinValue;
        private DateTime _lastWrite = DateTime.MinValue;
        private string _lastCore = "";

        private string _song = "", _author = "", _level = "";
        private int _diff;
        private int _score, _perfect, _great, _miss, _early, _late, _combo;
        private float _acc;
        private string _state = "idle";
        private string _error = "";
        private IntPtr _taskCache;

        private static string OutputDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MuseDashTrak");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("MuseDashTrak " + BuildInfo.Version + " loaded; output: " + OutputDir);
            try { Publish(true); } catch { }
        }

        public override void OnUpdate()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _lastPoll).TotalSeconds < PollInterval)
                return;
            _lastPoll = now;

            try
            {
                if (!_resolved)
                    Resolve();
                if (!_resolved)
                {
                    if (_resolveAttempts > 0 && _resolveAttempts % 20 == 0)
                        Publish(true);
                    return;
                }

                Poll();
                if ((now - _lastWrite).TotalMilliseconds > 350)
                    Publish(false);
            }
            catch (Exception e)
            {
                LoggerInstance.Warning("poll failed: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private void Resolve()
        {
            Api.LoadImages();
            _clsDb = Api.FindClass("Assets.Scripts.Database", "GlobalDataBase");
            _clsStage = Api.FindClass("FormulaBase", "StageBattleComponent");
            // Muse Dash 6.8 moved TaskStageTarget; accept both namespaces.
            _clsTask = Api.FindClass("Assets.Scripts.GameCore.HostComponent", "TaskStageTarget");
            if (_clsTask == IntPtr.Zero)
                _clsTask = Api.FindClass("GameLogic", "TaskStageTarget");
            _clsRole = Api.FindClass("Assets.Scripts.GameCore.HostComponent", "BattleRoleAttributeComponent");
            _clsMusicInfo = Api.FindClass("Assets.Scripts.Database", "MusicInfo");

            if (_clsStage == IntPtr.Zero || _clsTask == IntPtr.Zero || _clsDb == IntPtr.Zero)
            {
                _resolveAttempts++;
                _error = "unresolved";
                if (_resolveAttempts == 30 && !_dumpDone)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("MuseDashTrak unresolved diag " + DateTime.Now.ToString("s"));
                        sb.AppendLine("db=" + (_clsDb != IntPtr.Zero) + " stage=" + (_clsStage != IntPtr.Zero) +
                                      " task=" + (_clsTask != IntPtr.Zero) + " role=" + (_clsRole != IntPtr.Zero) +
                                      " music=" + (_clsMusicInfo != IntPtr.Zero));
                        Api.DumpCandidateClasses(sb);
                        File.WriteAllText(Path.Combine(OutputDir, "diag_unresolved.txt"), sb.ToString(), Encoding.UTF8);
                        LoggerInstance.Warning("could not resolve game classes - see diag_unresolved.txt");
                    }
                    catch { }
                }
                return;
            }

            _resolved = true;
            _error = "";
            LoggerInstance.Msg("classes: db=" + Api.ClassName(_clsDb) + " stage=" + Api.ClassName(_clsStage) +
                               " task=" + Api.ClassName(_clsTask) + " role=" + Api.ClassName(_clsRole) +
                               " music=" + Api.ClassName(_clsMusicInfo));

            if (_dumpDone)
                return;
            _dumpDone = true;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("MuseDashTrak diag " + DateTime.Now.ToString("s"));
                sb.AppendLine("db.s_DbBattleStage: " + (Api.FindField(_clsDb, "s_DbBattleStage") != IntPtr.Zero ? "ok" : "MISSING"));
                sb.AppendLine("task hierarchy: " + Api.Hierarchy(_clsTask));
                sb.AppendLine("stage hierarchy: " + Api.Hierarchy(_clsStage));
                sb.AppendLine("role hierarchy: " + Api.Hierarchy(_clsRole));
                Api.DumpMembers(_clsStage, sb);
                Api.DumpMembers(_clsTask, sb);
                Api.DumpMembers(_clsRole, sb);
                Api.DumpMembers(_clsMusicInfo, sb);
                Api.DumpMethods(_clsStage, sb);
                Api.DumpMethods(_clsTask, sb);
                Api.DumpMethods(_clsRole, sb);
                File.WriteAllText(Path.Combine(OutputDir, "diag.txt"), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private void Poll()
        {
            IntPtr stage = Api.Singleton(_clsStage, "instance");
            IntPtr task = Api.Singleton(_clsTask, "instance");
            if (task == IntPtr.Zero)
            {
                task = _taskCache;
                if (task == IntPtr.Zero)
                {
                    task = Api.FindLiveObject(_clsTask);
                    if (task != IntPtr.Zero)
                        LoggerInstance.Msg("TaskStageTarget found via FindObjectOfType");
                }
            }
            _taskCache = task;

            ReadSongInfo();

            if (stage == IntPtr.Zero && task == IntPtr.Zero)
            {
                if (_state == "playing")
                {
                    _state = "result";
                    _lastPlaying = DateTime.UtcNow;
                    _taskCache = IntPtr.Zero;
                }
                else if (_state == "result" && (DateTime.UtcNow - _lastPlaying).TotalSeconds > 30)
                {
                    _state = "idle";
                    _song = "";
                }
                return;
            }

            if (_state != "playing")
            {
                _battleStart = DateTime.UtcNow;
                _state = "playing";
                _score = _perfect = _great = _miss = _early = _late = _combo = 0;
                _acc = 0;
            }
            _lastPlaying = DateTime.UtcNow;

            if (task != IntPtr.Zero)
            {
                _score = Api.MemberInt(task, _clsTask, "m_Score", _score);
                _perfect = Api.MemberInt(task, _clsTask, "m_PerfectResult", _perfect);
                _great = Api.MemberInt(task, _clsTask, "m_GreatResult", _great);
                float acc = Api.InvokeFloat(task, _clsTask, "GetAccuracy", -1f);
                if (acc >= 0f)
                    _acc = acc * 100f;
            }

            if (_clsRole != IntPtr.Zero)
            {
                IntPtr role = Api.Singleton(_clsRole, "instance");
                if (role != IntPtr.Zero)
                {
                    _early = Api.MemberInt(role, _clsRole, "early", _early);
                    _late = Api.MemberInt(role, _clsRole, "late", _late);
                }
            }

            _combo = ReadCombo(stage, task);
        }

        private int ReadCombo(IntPtr stage, IntPtr task)
        {
            foreach (string n in new[] { "m_Combo", "m_ComboCount", "m_ComboNum", "combo", "m_MaxCombo" })
            {
                int v = Api.MemberInt(task, _clsTask, n, int.MinValue);
                if (v != int.MinValue)
                    return v;
                v = Api.MemberInt(stage, _clsStage, n, int.MinValue);
                if (v != int.MinValue)
                    return v;
            }
            return _combo;
        }

        private void ReadSongInfo()
        {
            if (_clsDb == IntPtr.Zero)
                return;
            IntPtr dbStage = Api.Singleton(_clsDb, "s_DbBattleStage");
            if (dbStage == IntPtr.Zero)
                return;
            IntPtr dbClass = Api.il2cpp_object_get_class(dbStage);

            IntPtr music = Api.MemberObject(dbStage, dbClass, "selectedMusicInfo");
            _diff = Api.MemberInt(dbStage, dbClass, "selectedDifficulty", _diff);

            if (music == IntPtr.Zero)
                return;
            if (_clsMusicInfo == IntPtr.Zero)
                _clsMusicInfo = Api.il2cpp_object_get_class(music);

            string name = Api.MemberString(music, _clsMusicInfo, "name");
            if (!string.IsNullOrEmpty(name))
                _song = name;
            string author = Api.MemberString(music, _clsMusicInfo, "author");
            if (!string.IsNullOrEmpty(author))
                _author = author;
            string level = string.Empty;
            if (_diff >= 1 && _diff <= 5)
                level = Api.MemberString(music, _clsMusicInfo, "difficulty" + _diff);
            if (string.IsNullOrEmpty(level))
                level = Api.InvokeIntString(music, _clsMusicInfo, "GetMusicLevelStringByDiff", _diff);
            if (!string.IsNullOrEmpty(level))
                _level = level.Trim();
        }

        private void Publish(bool force)
        {
            string core = BuildJson(false);
            DateTime now = DateTime.UtcNow;
            bool changed = core != _lastCore;
            // Keep the file fresh (the bridge treats >20s as stale) but don't
            // rewrite it ~3x/second while nothing is happening.
            if (!force && !changed && (now - _lastWrite).TotalSeconds < 5)
                return;
            _lastCore = core;
            _lastWrite = now;

            string dir = OutputDir;
            string tmp = Path.Combine(dir, "status.tmp");
            File.WriteAllText(tmp, BuildJson(true), new UTF8Encoding(false));
            string final = Path.Combine(dir, "status.json");
            try
            {
                if (File.Exists(final))
                    File.Replace(tmp, final, null);
                else
                    File.Move(tmp, final);
            }
            catch
            {
                try { File.Copy(tmp, final, true); File.Delete(tmp); } catch { }
            }
        }

        private string BuildJson(bool includeUpdated)
        {
            double elapsed = _state == "playing" && _battleStart != DateTime.MinValue
                ? (DateTime.UtcNow - _battleStart).TotalSeconds
                : 0;

            var sb = new StringBuilder(256);
            sb.Append('{');
            sb.Append("\"mod\":\"").Append(BuildInfo.Version).Append("\",");
            sb.Append("\"state\":\"").Append(_state).Append("\",");
            if (_error.Length > 0) sb.Append("\"error\":\"").Append(_error).Append("\",");
            sb.Append("\"song\":\"").Append(Esc(_song)).Append("\",");
            sb.Append("\"author\":\"").Append(Esc(_author)).Append("\",");
            sb.Append("\"diff\":").Append(_diff).Append(',');
            sb.Append("\"level\":\"").Append(Esc(_level)).Append("\",");
            sb.Append("\"score\":").Append(_score).Append(',');
            sb.Append("\"acc\":").Append(_acc.ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"combo\":").Append(_combo).Append(',');
            sb.Append("\"perfect\":").Append(_perfect).Append(',');
            sb.Append("\"great\":").Append(_great).Append(',');
            sb.Append("\"early\":").Append(_early).Append(',');
            sb.Append("\"late\":").Append(_late).Append(',');
            sb.Append("\"miss\":").Append(_miss).Append(',');
            sb.Append("\"elapsed\":").Append(elapsed.ToString("0.0", CultureInfo.InvariantCulture));
            if (includeUpdated)
                sb.Append(",\"updated\":").Append((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
            sb.Append('}');
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        public override void OnApplicationQuit()
        {
            try
            {
                _state = "idle";
                _song = "";
                Publish(true);
            }
            catch { }
        }
    }

    internal static class BuildInfo
    {
        public const string Version = "0.1.0";
    }
}
