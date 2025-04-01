using BlackBytesBox.Routed.GitBackend.Utility.ProcessUtility;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware
{

    public static partial class IServiceCollectionExtensions
    {
        public static IServiceCollection AddBackendSettings(this IServiceCollection services,string filePath, Action<BackendSettings>? manualConfigure = null)
        {
            var backendSettings = new DynamicSettingsService<BackendSettings>(filePath);

            // Optionally apply additional code configuration.
            if (manualConfigure != null)
            {
                backendSettings.UpdateSettings(currentSettings =>
                {
                    manualConfigure(currentSettings);
                    return currentSettings;
                });
            }

            var setting = backendSettings.CurrentSettings;

            var repoList = setting.AccessRights.Select(e=>e.Path).ToList();
            foreach (var repo in repoList)
            {
                var segements = repo.Split('/');
                List<string> gitRepoPathSegements = segements.TakeWhile(e => !e.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList();
                string gitRepoName = segements.Where(s => s.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList().First();
                var repoDepth = gitRepoPathSegements.Count;
                
                string gitDepthRepoPath = System.IO.Path.Combine(new[] { setting.BackendRoot, repoDepth.ToString() }.Concat(gitRepoPathSegements).ToArray());

                if (!System.IO.Directory.Exists(Path.Combine(gitDepthRepoPath, gitRepoName)))
                {
                    System.IO.Directory.CreateDirectory(gitDepthRepoPath);
                    var result = ProcessUtility.ExecuteProcess(@$"git", @$"-C ""{gitDepthRepoPath}"" init --bare {gitRepoName}", "");
                    var result2 = ProcessUtility.ExecuteProcess(@"git", @$"-C ""{Path.Combine(gitDepthRepoPath, gitRepoName)}"" config http.receivepack true", "");
                }
            }


            //string gitDepthRepoPath = System.IO.Path.Combine(new[] { setting.BackendRoot, repoDepth.ToString() }.Concat(gitRepoPathSegements).ToArray());



            services.AddSingleton<DynamicSettingsService<BackendSettings>>(backendSettings);

            return services;
        }
    }
}