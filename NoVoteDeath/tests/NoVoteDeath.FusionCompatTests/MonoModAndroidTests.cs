using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Xunit;

namespace NoVoteDeath.FusionCompatTests
{
    /// <summary>
    /// Integração com as DLLs REAIS do FusionCore (MonoMod.Core 1.4.0-alpha, HarmonyX 2.16, BepInEx Fusion) e a
    /// NoVoteDeath.dll compilada, executadas no .NET 10.
    /// Reproduz o erro do LogOutput.log ("Access to the path '/data/.cache' is denied" em AndroidSystem..ctor)
    /// instanciando o próprio MonoMod.Core.Platforms.Systems.AndroidSystem, e verifica a correção do plugin.
    /// Muda a variável HOME do processo, por isso não roda em paralelo com outros testes.
    /// </summary>
    [CollectionDefinition("env", DisableParallelization = true)]
    public class EnvCollection { }

    [Collection("env")]
    public class MonoModAndroidTests
    {
        private static readonly string Dir = typeof(MonoModAndroidTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .First(a => a.Key == "FusionCoreDir").Value!;

        /// <summary>Carrega as DLLs do FusionCore + plugin num contexto próprio (como o BepInEx faz no aparelho).</summary>
        private sealed class FusionAlc : AssemblyLoadContext
        {
            private readonly string[] _dirs;
            public FusionAlc() : base("fusioncore-test", isCollectible: false)
            {
                _dirs = new[] { Path.Combine(Dir, "BepInEx", "core"), Path.Combine(Dir, "out"), Path.Combine(Dir, "interop") };
            }
            protected override Assembly? Load(AssemblyName name)
            {
                foreach (var d in _dirs)
                {
                    var p = Path.Combine(d, name.Name + ".dll");
                    if (File.Exists(p)) return LoadFromAssemblyPath(p);
                }
                return null;
            }
        }

        // MonoMod.Core.Interop.Android chama a libc do Android (bionic). Para rodar o AndroidSystem REAL aqui (glibc),
        // um shim mínimo traduz as 2 diferenças de ABI que o construtor usa: __errno (glibc: __errno_location) e
        // sysconf(_SC_PAGESIZE) (bionic = 40, glibc = 30). Todo o resto (mmap, pipe2...) vem da glibc.
        private const string ShimSource = @"
#define _GNU_SOURCE
#include <dlfcn.h>
#include <errno.h>
#include <unistd.h>
int *__errno(void) { return &errno; }
long sysconf(int name) {
    static long (*real)(int);
    if (!real) real = (long (*)(int))dlsym(RTLD_NEXT, ""sysconf"");
    return real(name == 40 ? _SC_PAGESIZE : name);
}";

        private static string BuildShim()
        {
            string dir = Path.Combine(Path.GetTempPath(), "nvd-bionic-shim");
            Directory.CreateDirectory(dir);
            string src = Path.Combine(dir, "shim.c"), so = Path.Combine(dir, "libnvdbionicshim.so");
            File.WriteAllText(src, ShimSource);
            var psi = new System.Diagnostics.ProcessStartInfo("cc", $"-shared -fPIC -O2 -o \"{so}\" \"{src}\" -ldl")
                { RedirectStandardError = true, UseShellExecute = false };
            using var p = System.Diagnostics.Process.Start(psi)!;
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            Assert.True(p.ExitCode == 0 && File.Exists(so), "Precisa de um compilador C (cc) para o shim bionic: " + err);
            return so;
        }

        private static readonly Lazy<FusionAlc> Alc = new(() =>
        {
            var alc = new FusionAlc();
            string shim = BuildShim();
            var core = alc.LoadFromAssemblyName(new AssemblyName("MonoMod.Core"));
            NativeLibrary.SetDllImportResolver(core, (name, asm, path) =>
                name == "libc" ? NativeLibrary.Load(shim) : IntPtr.Zero);
            return alc;
        });

        private static Assembly A(string name) => Alc.Value.LoadFromAssemblyName(new AssemblyName(name));

        private static object NewAndroidSystem()
        {
            var t = A("MonoMod.Core").GetType("MonoMod.Core.Platforms.Systems.AndroidSystem", throwOnError: true)!;
            try { return Activator.CreateInstance(t, nonPublic: true)!; }
            catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException!).Throw(); throw; }
        }

        private static string? HelperDropPath()
        {
            var sw = A("MonoMod.Utils").GetType("MonoMod.Switches", throwOnError: true)!;
            var args = new object?[] { "HelperDropPath", null };
            bool ok = (bool)sw.GetMethod("TryGetSwitchValue")!.Invoke(null, args)!;
            return ok ? args[1] as string : null;
        }

        private static IDisposable PluginPrepare()
        {
            var log = Activator.CreateInstance(A("BepInEx.Core").GetType("BepInEx.Logging.ManualLogSource", true)!, "teste")!;
            var fix = A("NoVoteDeath").GetType("NoVoteDeath.MonoModAndroidFix", throwOnError: true)!;
            return (IDisposable)fix.GetMethod("Prepare", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new[] { log })!;
        }

        /// Pasta que EXISTE mas onde nem root consegue criar subpastas — o papel de "/data" para um app Android.
        /// No .NET de Linux, LocalApplicationData = $XDG_DATA_HOME; no CoreLib do FusionCore, = $HOME.
        private const string Denied = "/proc";

        private static T WithEnv<T>(string? home, string? xdg, Func<T> body)
        {
            string? oldHome = Environment.GetEnvironmentVariable("HOME"), oldXdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            try
            {
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", xdg);
                Environment.SetEnvironmentVariable("HOME", home);
                return body();
            }
            finally
            {
                Environment.SetEnvironmentVariable("HOME", oldHome);
                Environment.SetEnvironmentVariable("XDG_DATA_HOME", oldXdg);
            }
        }

        [Fact]
        public void SemCorrecao_AndroidSystemFalhaAoCriarCache_ComoNoLog()
        {
            var ex = WithEnv(Denied, Denied, () =>
            {
                Assert.Equal(Denied, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                return Record.Exception(() => NewAndroidSystem());
            });
            Assert.NotNull(ex);
            Assert.True(ex is UnauthorizedAccessException || ex is IOException, ex.ToString());
            Assert.Contains("/proc/.cache", ex.Message);          // no aparelho: "/data/.cache"
            Assert.Contains(ex.StackTrace!.Split('\n'), l => l.Contains("AndroidSystem..ctor"));
        }

        [Fact]
        public void ComCorrecaoDoPlugin_AndroidSystemInicializa_EHomeEhRestaurado()
        {
            WithEnv(Denied, Denied, () =>
            {
                string? helper, home;
                using (PluginPrepare())
                {
                    home = Environment.GetEnvironmentVariable("HOME");
                    Assert.NotEqual(Denied, home);
                    Assert.Equal(home, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                    NewAndroidSystem();                    // não lança mais
                    helper = HelperDropPath();
                }
                Assert.Equal(Path.Combine(home!, ".cache"), helper);   // HelperDropPath = pasta interna gravável
                Assert.True(Directory.Exists(helper), helper);
                Assert.True(Core_CanWrite(helper!));
                Assert.Equal(Denied, Environment.GetEnvironmentVariable("HOME"));          // tudo restaurado
                Assert.Equal(Denied, Environment.GetEnvironmentVariable("XDG_DATA_HOME"));
                Assert.Equal(Denied, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                return 0;
            });
        }

        [Fact]
        public void HomeJaGravavel_PluginNaoMexeEmNada()
        {
            string home = Path.Combine(Path.GetTempPath(), "nvd-home-ok-" + Guid.NewGuid().ToString("N"));
            WithEnv(home, null, () =>
            {
                // No .NET de Linux, LocalApplicationData = $HOME/.local/share e GetFolderPath devolve "" se não existir.
                Directory.CreateDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify));
                using (PluginPrepare()) Assert.Equal(home, Environment.GetEnvironmentVariable("HOME"));
                Assert.Equal(home, Environment.GetEnvironmentVariable("HOME"));
                return 0;
            });
        }

        /// HarmonyX 2.16 + MonoMod do FusionCore funcionam no .NET 10 (CoreCLR): Harmony.Patch aplica um Postfix de verdade.
        [Fact]
        public void HarmonyDoFusionCore_AplicaPostfixNoNet10()
        {
            var harmonyAsm = A("0Harmony");
            var harmonyType = harmonyAsm.GetType("HarmonyLib.Harmony", true)!;
            var hm = harmonyAsm.GetType("HarmonyLib.HarmonyMethod", true)!;
            var harmony = Activator.CreateInstance(harmonyType, "nvd.test")!;
            var original = typeof(Alvo).GetMethod(nameof(Alvo.Valor))!;
            var postfix = Activator.CreateInstance(hm, typeof(Alvo).GetMethod(nameof(Alvo.Postfix)))!;
            var patch = harmonyType.GetMethods().Single(m => m.Name == "Patch" && m.GetParameters().Length == 5);
            Assert.Equal(1, Alvo.Valor());
            patch.Invoke(harmony, new object?[] { original, null, postfix, null, null });
            Assert.Equal(42, Alvo.Valor());
            harmonyType.GetMethod("UnpatchSelf")!.Invoke(harmony, null);
        }

        public static class Alvo
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public static int Valor() => 1;
            public static void Postfix(ref int __result) => __result = 42;
        }

        private static bool Core_CanWrite(string dir)
        {
            var p = Path.Combine(dir, "w" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(p, "x"); File.Delete(p); return true;
        }
    }
}
