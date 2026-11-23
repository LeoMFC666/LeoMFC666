using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace NoVoteDeath.FusionCompatTests
{
    /// <summary>
    /// Confere, contra as DLLs REAIS que o FusionCore carrega no Android, tudo o que só falharia em tempo de execução:
    /// alvos do Harmony por string, nomes de parâmetros injetados nos Postfix, constantes do jogo e as
    /// versões de BepInEx/Il2CppInterop/HarmonyX/.NET referenciadas pela NoVoteDeath.dll.
    /// </summary>
    public sealed class Fixture : IDisposable
    {
        public readonly string Dir;
        public readonly MetadataLoadContext Mlc;
        public readonly Assembly Game, Plugin, Dummy;
        private readonly MetadataLoadContext _dummyMlc;
        public readonly Dictionary<string, Version> FusionVersions = new();

        public Fixture()
        {
            Dir = typeof(Fixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .First(a => a.Key == "FusionCoreDir").Value!;
            string core = Path.Combine(Dir, "BepInEx", "core"), interop = Path.Combine(Dir, "interop");
            string plugin = Path.Combine(Dir, "out", "NoVoteDeath.dll");
            foreach (var p in new[] { core, interop, plugin })
                Assert.True(Directory.Exists(p) || File.Exists(p), $"Falta {p}: rode build/build.sh");

            var paths = Directory.GetFiles(core, "*.dll").Concat(Directory.GetFiles(interop, "*.dll"))
                .Concat(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll"))
                .GroupBy(Path.GetFileName).Select(g => g.First()).Append(plugin);
            Mlc = new MetadataLoadContext(new PathAssemblyResolver(paths));
            Game = Mlc.LoadFromAssemblyPath(Path.Combine(interop, "Assembly-CSharp.dll"));
            Plugin = Mlc.LoadFromAssemblyPath(plugin);
            // Assemblies "dummy" do AmongUs.GameLibs.Android (fonte do interop): é onde ficam os valores literais das constantes.
            string dummy = Path.Combine(Dir, "dummy");
            Assert.True(Directory.Exists(dummy), $"Falta {dummy}: rode build/gen-interop.sh");
            _dummyMlc = new MetadataLoadContext(new PathAssemblyResolver(Directory.GetFiles(dummy, "*.dll")
                .Concat(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll"))
                .GroupBy(Path.GetFileName).Select(g => g.First())));
            Dummy = _dummyMlc.LoadFromAssemblyPath(Path.Combine(dummy, "Assembly-CSharp.dll"));
            foreach (var f in Directory.GetFiles(core, "*.dll"))
            {
                try { var n = AssemblyName.GetAssemblyName(f); FusionVersions[n.Name!] = n.Version!; } catch (BadImageFormatException) { }
            }
        }

        public Type T(string name) => Game.GetType(name, throwOnError: true)!;
        public Type D(string name) => Dummy.GetType(name, throwOnError: true)!;
        public void Dispose() { Mlc.Dispose(); _dummyMlc.Dispose(); }
    }

    public class FusionCoreCompatTests : IClassFixture<Fixture>
    {
        private readonly Fixture _f;
        public FusionCoreCompatTests(Fixture f) => _f = f;

        private static object? Const(Type t, string field) =>
            t.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue();

        // ---------- Alvos do Harmony (MeetingPatches.cs) ----------

        public static IEnumerable<object[]> HarmonyPatches()
        {
            yield return new object[] { "NoVoteDeath.MeetingStartPatch" };
            yield return new object[] { "NoVoteDeath.VotingCompletePatch" };
            yield return new object[] { "NoVoteDeath.MeetingTimerPatch" };
        }

        /// O Harmony resolve [HarmonyPatch(typeof(X), "Nome")] por nome: precisa existir exatamente 1 método,
        /// e cada parâmetro do Postfix (exceto __instance/__result...) precisa ter o mesmo nome e tipo no original.
        [Theory, MemberData(nameof(HarmonyPatches))]
        public void AlvoDoHarmony_ExisteEParametrosBatem(string patchClass)
        {
            var patch = _f.Plugin.GetType(patchClass, throwOnError: true)!;
            var attr = patch.GetCustomAttributesData().Single(a => a.AttributeType.Name == "HarmonyPatch");
            var target = (Type)attr.ConstructorArguments[0].Value!;
            var name = (string)attr.ConstructorArguments[1].Value!;

            var candidates = target.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == name).ToList();
            Assert.True(candidates.Count == 1, $"{target.Name}.{name}: {candidates.Count} sobrecargas no interop do FusionCore");
            var original = candidates[0];

            var postfix = patch.GetMethod("Postfix", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.NotNull(postfix);
            foreach (var p in postfix.GetParameters())
            {
                if (p.Name == "__instance") { Assert.Equal(target.FullName, p.ParameterType.FullName); continue; }
                var match = original.GetParameters().SingleOrDefault(o => o.Name == p.Name);
                Assert.True(match != null, $"Postfix de {name} pede '{p.Name}', que não existe em {target.Name}.{name}");
                Assert.Equal(match!.ParameterType.FullName, p.ParameterType.FullName);
            }
        }

        [Fact]
        public void VotingComplete_TemAssinaturaEsperada()
        {
            var m = _f.T("MeetingHud").GetMethod("VotingComplete", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            Assert.Equal(new[] { "states", "exiled", "tie", "wasOverruled", "overruleNonce" }, m.GetParameters().Select(p => p.Name));
        }

        // ---------- Constantes e membros usados (VoteEvaluator / Eliminator / MeetingPatches) ----------

        [Fact]
        public void ConstantesDeVoto_IguaisAoJogo()
        {
            var pva = _f.D("PlayerVoteArea");
            Assert.Equal((byte)255, Const(pva, "HasNotVoted"));
            Assert.Equal((byte)254, Const(pva, "MissedVote"));
            Assert.Equal((byte)253, Const(pva, "SkippedVote"));
            Assert.Equal((byte)252, Const(pva, "DeadVote"));
        }

        [Fact]
        public void RpcCallsExiled_Vale4()
        {
            var rpc = _f.D("RpcCalls");
            Assert.Equal(4, Convert.ToInt32(Const(rpc, "Exiled")));
        }

        [Fact]
        public void EnumsUsados_Existem()
        {
            Assert.NotNull(_f.T("MeetingHud+MeetingStates").GetField("NotVoted"));
            Assert.NotNull(_f.T("StringNames").GetField("MeetingVotingEnds"));
        }

        // ---------- Pilha do FusionCore ----------

        [Fact]
        public void Plugin_ReferenciaExatamenteAsVersoesDoFusionCore()
        {
            foreach (var r in _f.Plugin.GetReferencedAssemblies())
            {
                if (!_f.FusionVersions.TryGetValue(r.Name!, out var have)) continue;
                Assert.True(r.Version == have, $"{r.Name}: plugin referencia {r.Version}, FusionCore traz {have}");
            }
            var names = _f.Plugin.GetReferencedAssemblies().Select(r => r.Name).ToList();
            Assert.Contains("BepInEx.Unity.IL2CPP", names);
            Assert.Contains("0Harmony", names);
            Assert.Contains("Il2CppInterop.Runtime", names);
        }

        [Fact]
        public void FusionCore_EhBepInExFusionComIl2CppInterop152EHarmonyX216()
        {
            var bep = _f.Mlc.LoadFromAssemblyPath(Path.Combine(_f.Dir, "BepInEx", "core", "BepInEx.Unity.IL2CPP.dll"));
            var info = bep.GetCustomAttributesData().First(a => a.AttributeType.Name == "AssemblyInformationalVersionAttribute");
            Assert.StartsWith("6.0.0-fusion.", (string)info.ConstructorArguments[0].Value!);
            Assert.Equal(new Version(1, 5, 2, 0), _f.FusionVersions["Il2CppInterop.Runtime"]);
            Assert.Equal(new Version(2, 16, 0, 0), _f.FusionVersions["0Harmony"]);
        }

        [Fact]
        public void Plugin_EhNet10_AnyCpu_ComBepInPluginCorreto()
        {
            var tfm = _f.Plugin.GetCustomAttributesData().First(a => a.AttributeType.Name == "TargetFrameworkAttribute");
            Assert.Equal(".NETCoreApp,Version=v10.0", tfm.ConstructorArguments[0].Value);

            var mod = _f.Plugin.ManifestModule;
            mod.GetPEKind(out var kind, out var machine);
            Assert.True(kind.HasFlag(PortableExecutableKinds.ILOnly) && !kind.HasFlag(PortableExecutableKinds.Required32Bit), $"PE: {kind}");

            var plugin = _f.Plugin.GetType("NoVoteDeath.NoVoteDeathPlugin", true)!;
            Assert.Equal("BasePlugin", plugin.BaseType!.Name);
            var bp = plugin.GetCustomAttributesData().Single(a => a.AttributeType.Name == "BepInPlugin");
            Assert.Equal("dev.novotedeath.mod", bp.ConstructorArguments[0].Value);
            Assert.Equal("NO VOTE = DEATH", bp.ConstructorArguments[1].Value);
        }

        [Fact]
        public void Plugin_NaoUsaMonoModDiretamente()
        {
            // README do FusionCore: mods que usam MonoMod diretamente precisam migrar para o "MonoMod reorganized".
            Assert.DoesNotContain(_f.Plugin.GetReferencedAssemblies(), r => r.Name!.StartsWith("MonoMod"));
        }
    }
}
