using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.FileProviders;
using QuickEditer.Components;
using QuickEditer.Core;
using QuickEditer.Services;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.FileProviders;


namespace QuickEditer
{
    public class Program
    {
        public static string ShareDir { get; set; }

        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                ShareDir = args[0];
            }

            var builder = WebApplication.CreateBuilder();

#if RELEASE
            try
            {
                builder.WebHost.ConfigureKestrel(serverOptions =>
                {
                    if (Config.HasHttpsCert)
                    {
                        var cert = X509Certificate2.CreateFromPemFile(Config.CertPem, Config.CertKey);
                        serverOptions.ListenAnyIP(Config.Cfg.HttpPort, o => o.UseHttps(cert));
                    }
                    else
                    {
                        serverOptions.ListenAnyIP(Config.Cfg.HttpPort);
                    }
                });
            }
            catch
            {
                builder.WebHost.UseUrls($"http://0.0.0.0:{Config.Cfg.HttpPort}");
            }
#endif

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<CookieAuthStateProvider>();
            builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
                sp.GetRequiredService<CookieAuthStateProvider>());

            builder.Services.AddSignalR(o =>
            {
                o.MaximumReceiveMessageSize = 2048 * 1024;
            });

            builder.Services.AddSingleton<ProjectService>();
            builder.Services.AddSingleton<FileTreeService>();
            builder.Services.AddSingleton<ProjectTreeService>();
            builder.Services.AddSingleton<ArduinoCliService>();
            builder.Services.AddSingleton<ExampleService>();
            builder.Services.AddScoped<DialogService>();

            var app = builder.Build();

            Config.ReadConfig();

            var cliSvc = app.Services.GetRequiredService<ArduinoCliService>();
            cliSvc.EnsureEnvironment();

            var projectSvc = app.Services.GetRequiredService<ProjectService>();
            foreach (var p in projectSvc.ListProjects())
            {
                //try { projectSvc.CleanLogs(p, 20); } catch { }
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseHttpsRedirection();

     
            var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (Directory.Exists(webRoot))
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(webRoot)
                });
            }
            else
            {
                app.UseStaticFiles();
            }



            app.UseMiddleware<ApiAuthMiddleware>();
            app.UseAntiforgery();

            #region 路径校验

            bool IsPathAllowed(string path, string root)
            {
                try
                {
                    var full = Path.GetFullPath(path);
                    var fullRoot = Path.GetFullPath(root);
                    if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) return false;

                    if (File.Exists(full))
                    {
                        var info = new FileInfo(full);
                        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
                    }
                    else if (Directory.Exists(full))
                    {
                        var info = new DirectoryInfo(full);
                        if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            #endregion

            #region API

            app.MapGet("/api/project/export", (string name, ProjectService projectSvc) =>
            {
                var zip = projectSvc.ExportProjectZip(name);
                if (string.IsNullOrEmpty(zip) || !File.Exists(zip))
                    return Results.NotFound();

                return Results.File(zip, "application/zip", Path.GetFileName(zip));
            });

            app.MapGet("/api/file/download", (string path) =>
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return Results.NotFound();

                if (!IsPathAllowed(path, Config.ProjectsDir))
                    return Results.Forbid();

                var name = Path.GetFileName(path);
                return Results.File(path, "application/octet-stream", name);
            });

            app.MapGet("/api/file/image", (string path) =>
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return Results.NotFound();

                var ext = Path.GetExtension(path).ToLowerInvariant();
                var mime = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".svg" => "image/svg+xml",
                    ".webp" => "image/webp",
                    _ => ""
                };
                if (string.IsNullOrEmpty(mime)) return Results.BadRequest();

                if (!IsPathAllowed(path, Config.ProjectsDir) &&
                    !IsPathAllowed(path, Config.ArduinoCliDir))
                    return Results.Forbid();

                return Results.File(path, mime);
            });

            app.MapPost("/api/file/upload", async (HttpRequest request, string dir) =>
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    return Results.BadRequest("目标目录不存在");

                if (!IsPathAllowed(dir, Config.ProjectsDir))
                    return Results.Forbid();

                if (!request.HasFormContentType)
                    return Results.BadRequest("需要 multipart/form-data");

                var form = await request.ReadFormAsync();
                var saved = new List<string>();

                foreach (var file in form.Files)
                {
                    if (file.Length <= 0) continue;

                    var name = Path.GetFileName(file.FileName);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var dest = Path.Combine(dir, name);
                    if (File.Exists(dest))
                    {
                        var baseName = Path.GetFileNameWithoutExtension(name);
                        var ext = Path.GetExtension(name);
                        var i = 1;
                        while (File.Exists(dest))
                        {
                            dest = Path.Combine(dir, $"{baseName}_{i}{ext}");
                            i++;
                        }
                    }

                    await using var fs = File.Create(dest);
                    await file.CopyToAsync(fs);
                    saved.Add(Path.GetFileName(dest));
                }

                return Results.Ok(saved);
            });

            #endregion

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}