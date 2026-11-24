using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using NoVoteDeath.Core;

namespace NoVoteDeath
{
    /// <summary>
    /// Corrige "UnauthorizedAccessException: Access to the path '/data/.cache' is denied" em
    /// MonoMod.Core.Platforms.Systems.AndroidSystem..ctor() (via HarmonyLib NativeDetourMethodPatcher.TryResolve)
    /// no FusionCore. Análise completa em src/Core/MonoModHome.cs.
    ///
    /// Só enquanto o Harmony aplica os patches (é aí que o MonoMod cria a plataforma, uma vez por processo, e
    /// fixa HelperDropPath = &lt;LocalApplicationData&gt;/.cache), LocalApplicationData passa a apontar para uma
    /// pasta PRIVADA e INTERNA do app (gravável e sem noexec, porque o MonoMod faz dlopen de uma .so ali).
    /// Depois tudo volta ao que era. Não toca em diretórios protegidos nem no armazenamento compartilhado.
    /// Se a pasta atual já serve (PC/Linux com HOME normal), não faz nada.
    /// </summary>
    internal static class MonoModAndroidFix
    {
        private sealed class Nothing : IDisposable { public void Dispose() { } }

        public static IDisposable Prepare(ManualLogSource log)
        {
            if (Path.DirectorySeparatorChar != '/') return new Nothing(); // Windows: não se aplica

            // Mesmo valor que o MonoMod obteria (e que pode já estar memorizado no runtime).
            string current = SafeLocalAppData();
            var choice = MonoModHome.Choose(current,
                MonoModHome.Candidates(RuntimeDir(), PackageName(), Environment.GetEnvironmentVariable("TMPDIR")),
                MonoModHome.CanCreateAndWrite);

            if (!choice.Ok)
            {
                log.LogError($"[MonoMod/Android] {choice.Reason}. O Harmony vai falhar ao criar a pasta de cache do MonoMod.");
                return new Nothing();
            }
            if (choice.Home == null)
            {
                log.LogInfo($"[MonoMod/Android] {choice.Reason}");
                return new Nothing();
            }

            var cache = SpecialFolderCache();
            var redirect = new LocalAppDataRedirect(choice.Home, cache,
                Environment.GetEnvironmentVariable, Environment.SetEnvironmentVariable);

            string now = SafeLocalAppData();
            if (now == choice.Home)
                log.LogInfo($"[MonoMod/Android] {choice.Reason}");
            else // runtime sem o cache do FusionCore (ex.: .NET de Linux usa $HOME/.local/share): ainda é gravável
                log.LogWarning($"[MonoMod/Android] {choice.Reason}; LocalApplicationData agora = '{now}' (runtime não é o do FusionCore 0.045?)");
            return redirect;
        }

        /// Environment.s_specialFolders do CoreLib do FusionCore (null em runtimes que não têm esse cache).
        private static IDictionary<Environment.SpecialFolder, string> SpecialFolderCache()
        {
            try
            {
                var field = typeof(Environment).GetField("s_specialFolders", BindingFlags.NonPublic | BindingFlags.Static);
                if (field == null) return null;
                if (field.GetValue(null) == null) SafeLocalAppData(); // o runtime cria o dicionário na 1ª chamada
                return field.GetValue(null) as IDictionary<Environment.SpecialFolder, string>;
            }
            catch (Exception) { return null; }
        }

        private static string SafeLocalAppData()
        {
            try { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
            catch (Exception) { return null; }
        }

        // Pasta do runtime .NET extraído pelo FusionCore: /data/user/0/<fusioncore>/app_fusion_runtime/dotnet
        private static string RuntimeDir()
        {
            try { return Path.GetDirectoryName(typeof(object).Assembly.Location); }
            catch (Exception) { return null; }
        }

        private static string PackageName()
        {
            try { return MonoModHome.PackageFromCmdline(File.ReadAllText("/proc/self/cmdline")); }
            catch (Exception) { return null; }
        }
    }
}
