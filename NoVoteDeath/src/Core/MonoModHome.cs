using System;
using System.Collections.Generic;
using System.IO;

namespace NoVoteDeath.Core
{
    /// <summary>
    /// Escolhe um HOME gravável e executável para o MonoMod no Android (FusionCore).
    ///
    /// Por quê (conferido nas DLLs do FusionCore 0.045):
    ///  - MonoMod.Core 1.4.0-alpha, AndroidSystem..ctor():
    ///      dir = Path.Combine(Environment.GetFolderPath(LocalApplicationData), ".cache");
    ///      Directory.CreateDirectory(dir); Switches.SetSwitchValue("HelperDropPath", dir);
    ///    e depois grava "mm-exhelper.so.XXXXXX" nessa pasta (mkstemp) e faz dlopen dela.
    ///  - No System.Private.CoreLib do FusionCore, LocalApplicationData == HOME; sem a variável HOME o
    ///    runtime usa getpwuid(), que no Android devolve "/data" → "/data/.cache" → UnauthorizedAccessException.
    ///  - O HarmonyX 2.16 (NativeDetourMethodPatcher.TryResolve) acessa PlatformTriple.Current em TODO patch,
    ///    então qualquer Harmony.Patch dispara esse construtor.
    /// Logo a pasta precisa ser: gravável pelo app E em armazenamento interno (o externo é montado noexec e o
    /// dlopen falharia). Nada de diretórios protegidos: só pastas privadas do próprio app.
    /// Lógica pura + IO simples, testável fora do Android.
    /// </summary>
    public static class MonoModHome
    {
        /// <summary>Subpasta que o MonoMod cria dentro do HOME.</summary>
        public const string CacheFolderName = ".cache";

        /// <summary>Pasta criada por nós dentro do diretório privado do app para servir de HOME.</summary>
        public const string HomeFolderName = "monomod-home";

        // Armazenamento compartilhado do Android: gravável, mas noexec (dlopen da .so do MonoMod falharia).
        private static readonly string[] NoExecPrefixes = { "/storage/", "/sdcard/", "/mnt/", "/data/media/" };

        public static string CacheDirFor(string home) => Path.Combine(home, CacheFolderName);

        // Diretórios do sistema: nunca tentamos criar nada neles (o "/data" do erro original entra aqui).
        private static readonly string[] SystemPrefixes = { "/proc/", "/sys/", "/dev/", "/system/", "/vendor/", "/apex/", "/product/" };

        /// <summary>
        /// True só para caminhos absolutos que podem ser privados do app: rejeita, SEM acessar o disco,
        /// a raiz e pastas de 1º nível ("/", "/data"), diretórios do sistema e o armazenamento compartilhado (noexec).
        /// </summary>
        public static bool IsSuitableLocation(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("/", StringComparison.Ordinal)) return false;
            string p = path.EndsWith("/", StringComparison.Ordinal) ? path : path + "/";
            if (p.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Length < 2) return false;
            foreach (var prefix in SystemPrefixes)
                if (p.StartsWith(prefix, StringComparison.Ordinal)) return false;
            foreach (var prefix in NoExecPrefixes)
                if (p.StartsWith(prefix, StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>Nome do pacote a partir de /proc/self/cmdline ("pkg\0..." ou "pkg:processo").</summary>
        public static string PackageFromCmdline(string cmdline)
        {
            if (string.IsNullOrEmpty(cmdline)) return null;
            int end = cmdline.IndexOf('\0');
            string name = (end >= 0 ? cmdline.Substring(0, end) : cmdline).Trim();
            int colon = name.IndexOf(':');
            if (colon >= 0) name = name.Substring(0, colon);
            if (name.Length == 0 || name.IndexOf('/') >= 0 || name.IndexOf('.') < 0) return null; // não é um pacote
            return name;
        }

        /// <summary>
        /// Candidatos a HOME, todos privados do app, em ordem de preferência:
        ///  1) ao lado do runtime .NET que o FusionCore extraiu (Context.getDir("fusion_runtime"): interno, e o
        ///     próprio FusionCore já faz dlopen de bibliotecas de lá);
        ///  2) cache do app (/data/user/0/&lt;pacote&gt;/cache e /data/data/&lt;pacote&gt;/cache);
        ///  3) TMPDIR, se definido.
        /// </summary>
        public static List<string> Candidates(string runtimeDir, string packageName, string tmpDir)
        {
            var list = new List<string>();
            void Add(string p) { if (!string.IsNullOrEmpty(p) && !list.Contains(p)) list.Add(p); }

            if (!string.IsNullOrEmpty(runtimeDir))
            {
                string parent = Path.GetDirectoryName(runtimeDir.TrimEnd('/'));
                if (!string.IsNullOrEmpty(parent) && parent != "/") Add(Path.Combine(parent, HomeFolderName));
            }
            if (!string.IsNullOrEmpty(packageName))
            {
                Add($"/data/user/0/{packageName}/cache/{HomeFolderName}");
                Add($"/data/data/{packageName}/cache/{HomeFolderName}");
            }
            if (!string.IsNullOrEmpty(tmpDir)) Add(Path.Combine(tmpDir, HomeFolderName));
            return list;
        }

        /// <summary>Resultado da escolha. Home == null e Ok == true: o HOME atual já serve.</summary>
        public readonly struct Choice
        {
            public Choice(bool ok, string home, string reason) { Ok = ok; Home = home; Reason = reason; }
            public bool Ok { get; }
            public string Home { get; }
            public string Reason { get; }
        }

        /// <param name="currentHome">Valor que o MonoMod usaria (Environment.GetFolderPath(LocalApplicationData)).</param>
        /// <param name="canUseDir">Teste real: consegue criar a pasta e gravar nela.</param>
        public static Choice Choose(string currentHome, IEnumerable<string> candidates, Func<string, bool> canUseDir)
        {
            if (IsSuitableLocation(currentHome) && canUseDir(CacheDirFor(currentHome)))
                return new Choice(true, null, $"HOME atual serve: {CacheDirFor(currentHome)}");

            foreach (var c in candidates ?? Array.Empty<string>())
            {
                if (!IsSuitableLocation(c)) continue;
                if (canUseDir(CacheDirFor(c)))
                    return new Choice(true, c, $"'{CacheDirFor(currentHome ?? "")}' inutilizável; usando {CacheDirFor(c)}");
            }
            return new Choice(false, null, $"nenhuma pasta interna gravável encontrada (HOME atual: '{currentHome}')");
        }

        /// <summary>Cria a pasta e grava/apaga um arquivo nela. Nunca lança.</summary>
        public static bool CanCreateAndWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".nvd-probe-" + Guid.NewGuid().ToString("N"));
                File.WriteAllBytes(probe, new byte[] { 0 });
                File.Delete(probe);
                return true;
            }
            catch (Exception) // UnauthorizedAccessException, IOException, etc.
            {
                return false;
            }
        }
    }
}

namespace NoVoteDeath.Core
{
    /// <summary>
    /// Redireciona Environment.GetFolderPath(LocalApplicationData) para <c>home</c> e desfaz no Dispose.
    ///
    /// Não basta mudar HOME: o System.Private.CoreLib do FusionCore guarda o resultado de GetFolderPath em
    /// <c>Environment.s_specialFolders</c> (Dictionary&lt;SpecialFolder,string&gt;) na 1ª chamada. Se alguém já
    /// leu LocalApplicationData (p. ex. uma tentativa anterior do MonoMod), "/data" fica memorizado e trocar
    /// HOME não teria efeito. Por isso a entrada do cache também é trocada — e restaurada depois.
    /// Variáveis: HOME (CoreLib do FusionCore: LocalApplicationData == HOME) e XDG_DATA_HOME (no .NET padrão de
    /// Unix, LocalApplicationData == $XDG_DATA_HOME, senão $HOME/.local/share) — assim funciona nos dois runtimes.
    /// O acesso ao cache/variáveis é injetado (testável sem o runtime do FusionCore).
    /// </summary>
    public sealed class LocalAppDataRedirect : IDisposable
    {
        public static readonly string[] Variables = { "HOME", "XDG_DATA_HOME" };
        private const Environment.SpecialFolder Folder = Environment.SpecialFolder.LocalApplicationData;

        private readonly IDictionary<Environment.SpecialFolder, string> _cache;
        private readonly Action<string, string> _setEnv;
        private readonly string[] _previousEnv;
        private readonly bool _hadCached;
        private readonly string _previousCached;
        private bool _active = true;

        /// <param name="cache">Cache de pastas especiais do runtime, ou null se o runtime não tiver um.</param>
        public LocalAppDataRedirect(string home, IDictionary<Environment.SpecialFolder, string> cache,
                                    Func<string, string> getEnv, Action<string, string> setEnv)
        {
            _cache = cache;
            _setEnv = setEnv;
            _previousEnv = new string[Variables.Length];
            for (int i = 0; i < Variables.Length; i++)
            {
                _previousEnv[i] = getEnv(Variables[i]);
                setEnv(Variables[i], home);
            }
            if (_cache != null)
            {
                lock (_cache)
                {
                    _hadCached = _cache.TryGetValue(Folder, out _previousCached);
                    _cache[Folder] = home;
                }
            }
        }

        public void Dispose()
        {
            if (!_active) return;
            _active = false;
            for (int i = 0; i < Variables.Length; i++)
                _setEnv(Variables[i], _previousEnv[i]); // null = remove (estado original)
            if (_cache != null)
            {
                lock (_cache)
                {
                    if (_hadCached) _cache[Folder] = _previousCached;
                    else _cache.Remove(Folder);
                }
            }
        }
    }
}
