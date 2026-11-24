using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NoVoteDeath.Core;
using Xunit;

namespace NoVoteDeath.Tests
{
    public class MonoModHomeTests
    {
        private const string Pkg = "dev.allofus.fusioncore";
        private const string Runtime = "/data/user/0/dev.allofus.fusioncore/app_fusion_runtime/dotnet";

        // ---------- cenário do log: HOME ausente → LocalApplicationData = "/data" ----------

        [Fact] public void HomeData_NaoEhNemSondado_EUsaPastaPrivadaDoApp()
        {
            var probed = new List<string>();
            var c = MonoModHome.Choose("/data", MonoModHome.Candidates(Runtime, Pkg, null), d => { probed.Add(d); return true; });
            Assert.True(c.Ok);
            Assert.Equal("/data/user/0/dev.allofus.fusioncore/app_fusion_runtime/monomod-home", c.Home);
            Assert.DoesNotContain("/data/.cache", probed);                // diretório protegido nunca é tocado
            Assert.All(probed, p => Assert.StartsWith("/data/user/0/" + Pkg + "/", p));
        }

        [Fact] public void PrimeiroCandidatoFalha_UsaOProximo()
        {
            var c = MonoModHome.Choose("/data", MonoModHome.Candidates(Runtime, Pkg, null),
                d => d.StartsWith("/data/user/0/" + Pkg + "/cache/"));
            Assert.Equal("/data/user/0/" + Pkg + "/cache/monomod-home", c.Home);
        }

        [Fact] public void NenhumCandidatoGravavel_FalhaComMotivo()
        {
            var c = MonoModHome.Choose("/data", MonoModHome.Candidates(Runtime, Pkg, null), _ => false);
            Assert.False(c.Ok);
            Assert.Null(c.Home);
            Assert.Contains("/data", c.Reason);
        }

        [Fact] public void HomeAtualJaServe_NaoMuda()
        {
            var c = MonoModHome.Choose("/data/user/0/x.y/files", MonoModHome.Candidates(Runtime, Pkg, null), _ => true);
            Assert.True(c.Ok);
            Assert.Null(c.Home);
        }

        [Fact] public void HomeVazioOuRelativo_TrocaPorCandidato()
        {
            Assert.NotNull(MonoModHome.Choose("", new[] { "/data/user/0/a.b/cache/h" }, _ => true).Home);
            Assert.NotNull(MonoModHome.Choose(null, new[] { "/data/user/0/a.b/cache/h" }, _ => true).Home);
            Assert.NotNull(MonoModHome.Choose("rel/dir", new[] { "/data/user/0/a.b/cache/h" }, _ => true).Home);
        }

        // ---------- locais proibidos ----------

        [Theory]
        [InlineData("/")]
        [InlineData("/data")]
        [InlineData("/data/")]
        [InlineData("/storage/emulated/0/FusionCore/com.innersloth.spacemafia/BepInEx")]  // noexec: dlopen falharia
        [InlineData("/sdcard/Download")]
        [InlineData("/mnt/sdcard/x")]
        [InlineData("/data/media/0/x")]
        [InlineData("/proc/self/x")]
        [InlineData("/system/bin")]
        [InlineData("relativo/x")]
        [InlineData("")]
        public void LocaisInadequados_SaoRejeitadosSemAcessarDisco(string path) => Assert.False(MonoModHome.IsSuitableLocation(path));

        [Theory]
        [InlineData("/data/user/0/dev.allofus.fusioncore/cache/monomod-home")]
        [InlineData("/data/data/dev.allofus.fusioncore/app_fusion_runtime/monomod-home")]
        public void PastasPrivadasDoApp_SaoAceitas(string path) => Assert.True(MonoModHome.IsSuitableLocation(path));

        [Fact] public void ArmazenamentoExterno_NuncaEhEscolhido_MesmoGravavel()
        {
            var c = MonoModHome.Choose("/data", new[] { "/storage/emulated/0/FusionCore/h" }, _ => true);
            Assert.False(c.Ok);
        }

        // ---------- candidatos / pacote ----------

        [Fact] public void Candidatos_OrdemEsperada_SemDuplicatas()
        {
            var list = MonoModHome.Candidates(Runtime + "/", Pkg, "/data/local/tmp");
            Assert.Equal(new[]
            {
                "/data/user/0/dev.allofus.fusioncore/app_fusion_runtime/monomod-home",
                "/data/user/0/dev.allofus.fusioncore/cache/monomod-home",
                "/data/data/dev.allofus.fusioncore/cache/monomod-home",
                "/data/local/tmp/monomod-home",
            }, list);
        }

        [Fact] public void Candidatos_SemInformacao_ListaVazia() => Assert.Empty(MonoModHome.Candidates(null, null, null));

        [Theory]
        [InlineData("dev.allofus.fusioncore\0", "dev.allofus.fusioncore")]
        [InlineData("dev.allofus.fusioncore:game\0\0", "dev.allofus.fusioncore")]
        [InlineData("com.innersloth.spacemafia", "com.innersloth.spacemafia")]
        [InlineData("/system/bin/app_process64\0", null)]
        [InlineData("zygote", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void PacoteDoCmdline(string cmdline, string expected) => Assert.Equal(expected, MonoModHome.PackageFromCmdline(cmdline));

        // ---------- sonda real no disco ----------

        [Fact] public void Sonda_PastaTemporaria_Gravavel()
        {
            string d = Path.Combine(Path.GetTempPath(), "nvd-probe-" + Guid.NewGuid().ToString("N"), ".cache");
            Assert.True(MonoModHome.CanCreateAndWrite(d));
            Assert.Empty(Directory.GetFiles(d)); // não deixa lixo
            Directory.Delete(Path.GetDirectoryName(d), true);
        }

        [Fact] public void Sonda_CaminhoImpossivel_FalsoSemLancar()
        {
            string file = Path.GetTempFileName(); // um ARQUIVO no lugar da pasta-pai
            Assert.False(MonoModHome.CanCreateAndWrite(Path.Combine(file, ".cache")));
            File.Delete(file);
        }

        // ---------- redirecionamento (cache do CoreLib do FusionCore) ----------

        private static (Dictionary<string, string> env, Func<string, string> get, Action<string, string> set) Env(string home)
        {
            var env = new Dictionary<string, string>();
            if (home != null) env["HOME"] = home;
            return (env, k => env.TryGetValue(k, out var v) ? v : null, (k, v) => { if (v == null) env.Remove(k); else env[k] = v; });
        }

        [Fact] public void Redirect_ValorJaMemorizado_EhTrocadoERestaurado()
        {
            // Situação real: o runtime já memorizou "/data" (ex.: tentativa anterior do MonoMod). Só HOME não bastaria.
            var cache = new Dictionary<Environment.SpecialFolder, string> { [Environment.SpecialFolder.LocalApplicationData] = "/data" };
            var (env, get, set) = Env(null);
            using (new LocalAppDataRedirect("/data/user/0/p.q/cache/monomod-home", cache, get, set))
            {
                Assert.Equal("/data/user/0/p.q/cache/monomod-home", cache[Environment.SpecialFolder.LocalApplicationData]);
                Assert.Equal("/data/user/0/p.q/cache/monomod-home", env["HOME"]);
                Assert.Equal("/data/user/0/p.q/cache/monomod-home", env["XDG_DATA_HOME"]);
            }
            Assert.Equal("/data", cache[Environment.SpecialFolder.LocalApplicationData]);
            Assert.False(env.ContainsKey("HOME")); // HOME não existia → continua não existindo
            Assert.False(env.ContainsKey("XDG_DATA_HOME"));
        }

        [Fact] public void Redirect_SemValorMemorizado_RemoveAoFinal_EPreservaOutrasPastas()
        {
            var cache = new Dictionary<Environment.SpecialFolder, string> { [Environment.SpecialFolder.ApplicationData] = "/x/.config" };
            var (env, get, set) = Env("/home/u");
            var r = new LocalAppDataRedirect("/data/user/0/p.q/cache/h", cache, get, set);
            r.Dispose();
            r.Dispose(); // idempotente
            Assert.False(cache.ContainsKey(Environment.SpecialFolder.LocalApplicationData));
            Assert.Equal("/x/.config", cache[Environment.SpecialFolder.ApplicationData]);
            Assert.Equal("/home/u", env["HOME"]);
        }

        [Fact] public void Redirect_RuntimeSemCache_SoMexeNoHome()
        {
            var (env, get, set) = Env("/home/u");
            env["XDG_DATA_HOME"] = "/home/u/dados";
            using (new LocalAppDataRedirect("/data/user/0/p.q/cache/h", null, get, set))
                Assert.Equal("/data/user/0/p.q/cache/h", env["HOME"]);
            Assert.Equal("/home/u", env["HOME"]);
            Assert.Equal("/home/u/dados", env["XDG_DATA_HOME"]);
        }
    }
}
