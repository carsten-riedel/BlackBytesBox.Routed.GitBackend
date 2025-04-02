﻿using BlackBytesBox.Routed.GitBackend.Utility.ProcessUtility;

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
            filePath = Path.GetFullPath(filePath,AppContext.BaseDirectory);
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
            System.IO.Directory.CreateDirectory(setting.GitRepositorysDirectory);


            // Resolve empty paths
            if (string.IsNullOrWhiteSpace(setting.GitCommandFilePath))
            {
                var find = ProcessUtility.LocateExecutable("git");
                if (find == null)
                {
                    throw new Exception("Git executable not found in the system PATH.");
                }
                setting.GitCommandFilePath = find.Value.FullPath;
                setting.GitCommandRoot = Path.GetFullPath($@"{System.IO.Path.GetDirectoryName(setting.GitCommandFilePath)}\..");
            }

            if (string.IsNullOrWhiteSpace(setting.GitBackendFilePath))
            {
                var find = ProcessUtility.LocateExecutableRecursively("git-http-backend", setting.GitCommandRoot);
                if (find == null)
                {
                    throw new Exception($"git-http-backend not found in the {setting.GitBackendFilePath}");
                }
                setting.GitBackendFilePath = find.Value.FullPath;
            }

            backendSettings.UpdateSettings((settings) =>
            {
                settings.GitCommandRoot = setting.GitCommandRoot;
                settings.GitCommandFilePath = setting.GitCommandFilePath;
                settings.GitBackendFilePath = setting.GitBackendFilePath;
                return settings;
            });

            // Create git repositories
            var repoList = setting.AccessRights.Select(e=>e.Path).ToList();
            foreach (var repo in repoList)
            {
                var segements = repo.Split('/');
                List<string> gitRepoPathSegements = segements.TakeWhile(e => !e.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList();
                string gitRepoName = segements.Where(s => s.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList().First();
                var repoDepth = gitRepoPathSegements.Count;
                
                string gitDepthRepoPath = System.IO.Path.Combine(new[] { setting.GitRepositorysDirectory, repoDepth.ToString() }.Concat(gitRepoPathSegements).ToArray());

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