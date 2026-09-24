using System.Text.Json;
using System.Text.Json.Nodes;
using DataMaster.Data;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

// Migrasi otomatis alamat Hub API LAMA -> AppOptions.HubApiUrlResmi SAAT INI,
// dijalankan PALING AWAL (sebelum WebApplication.CreateBuilder membaca
// appsettings.json) - supaya PC yang SUDAH pernah Setup Awal (config lama
// tersimpan) ikut pindah otomatis kalau suatu saat VPS/domain Hub API
// berganti, TANPA staf mana pun perlu edit apapun manual - cukup tunggu
// auto-update jalan seperti biasa. PC yang belum pernah setup (Setup Awal
// belum pernah dijalankan) tidak terpengaruh sama sekali (AuthController yang
// mengisi bawaan utk kasus itu). Non-fatal SENGAJA - gagal baca/tulis di sini
// TIDAK BOLEH menghalangi aplikasi start sama sekali.
try
{
    var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    if (File.Exists(appSettingsPath) && AppOptions.HubApiUrlLama.Length > 0)
    {
        var json = File.ReadAllText(appSettingsPath);
        var root = JsonNode.Parse(json)?.AsObject();
        var urlSaatIni = root?["AppSettings"]?["HubApiUrl"]?.GetValue<string>();
        if (urlSaatIni is not null && Array.IndexOf(AppOptions.HubApiUrlLama, urlSaatIni.TrimEnd('/')) >= 0)
        {
            root!["AppSettings"]!["HubApiUrl"] = AppOptions.HubApiUrlResmi;
            File.WriteAllText(appSettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
catch { /* non-fatal - lihat komentar di atas */ }

// SELF-KONFIGURASI dari %LocalAppData%\DataMaster (2026-09-12, poin Windows
// Service) - SEBELUM ini, path database & kredensial Hub API HANYA pernah
// diisi lewat environment variable yang di-inject Launcher (WPF) saat
// menjalankan ini sbg ANAK PROSES (lihat ServerProcessManager.cs) - begitu
// app ini jadi Windows Service sungguhan (dijalankan Service Control
// Manager, TANPA Launcher jadi induknya sama sekali), tidak ada lagi yang
// meng-inject env var itu, app akan salah alamat database (balik ke
// App_Data/datamaster.db bawaan di DALAM folder instalasi - hilang/salah
// tiap auto-update).
//
// Diperbaiki: app ini SEKARANG membaca sendiri lokasi yang SAMA PERSIS yang
// sudah dipakai (tidak ada migrasi data, path fisiknya IDENTIK) - HANYA
// kalau env var belum diisi dari luar (`??=` semangatnya - kalau Launcher
// versi lama ATAU sesi tes manual sudah men-set env var duluan, itu tetap
// menang, tidak ditimpa - backward compatible penuh dgn cara lama & dgn
// pola tes ConnectionStrings__DataMaster=... yang dipakai sepanjang sesi
// pengembangan ini).
try
{
    var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster");
    var appDataDir = Path.Combine(dataDir, "App_Data");
    Directory.CreateDirectory(appDataDir);

    if (Environment.GetEnvironmentVariable("ConnectionStrings__DataMaster") is null)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DataMaster", $"Data Source={Path.Combine(appDataDir, "datamaster.db")}");
    }

    var hubApiConfigPath = Path.Combine(dataDir, "hubapi.json");
    if (File.Exists(hubApiConfigPath))
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(hubApiConfigPath));
        if (Environment.GetEnvironmentVariable("AppSettings__HubApiUrl") is null && doc.RootElement.TryGetProperty("HubApiUrl", out var u))
            Environment.SetEnvironmentVariable("AppSettings__HubApiUrl", u.GetString());
        if (Environment.GetEnvironmentVariable("AppSettings__HubApiToken") is null && doc.RootElement.TryGetProperty("HubApiToken", out var t))
            Environment.SetEnvironmentVariable("AppSettings__HubApiToken", t.GetString());
        // BackupPassphrase (2026-09-12, BUG NYATA - lihat UserSettingsViewModel.
        // BackupPassphraseAktif) - file JSON yang SAMA dgn Hub API (bukan file
        // baru), field baru saja. Dulu TIDAK ADA jalur apa pun mengisi nilai ini
        // (appsettings.json bawaan SELALU kosong) - backup awan diam2 tidak
        // pernah jalan di instalasi manapun sejak fitur ini ada.
        if (Environment.GetEnvironmentVariable("AppSettings__BackupPassphrase") is null && doc.RootElement.TryGetProperty("BackupPassphrase", out var bp))
            Environment.SetEnvironmentVariable("AppSettings__BackupPassphrase", bp.GetString());
        // Tipe Instalasi (2026-09-24, dipilih di Setup Awal utk mode Server) - `??=`
        // semangatnya SAMA PERSIS field lain di atas: env var Launcher (mode
        // "development" -> "pengembang", lihat ServerProcessManager.cs) TETAP
        // menang kalau sudah diisi dari luar, tidak pernah ditimpa file ini.
        if (Environment.GetEnvironmentVariable("AppSettings__InstallType") is null && doc.RootElement.TryGetProperty("InstallType", out var it))
            Environment.SetEnvironmentVariable("AppSettings__InstallType", it.GetString());
    }

    // service-config.json (2026-09-12) - port/alamat dengar Kestrel, ditulis
    // SEKALI oleh Launcher saat memasang Windows Service (lihat
    // ServerProcessManager.PasangServiceAsync). Windows Service TIDAK PUNYA
    // "proses induk" yang bisa inject ASPNETCORE_URLS via ProcessStartInfo
    // (beda dari anak proses biasa) - satu2nya cara service ini tahu port
    // mana yang harus didengarkan adalah baca sendiri dari file ini, pola
    // SAMA PERSIS hubapi.json di atas.
    var serviceConfigPath = Path.Combine(dataDir, "service-config.json");
    if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is null && File.Exists(serviceConfigPath))
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(serviceConfigPath));
        if (doc.RootElement.TryGetProperty("Port", out var p) && p.TryGetInt32(out var port))
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://0.0.0.0:{port}");
        }
    }
}
catch { /* non-fatal - kalau gagal baca, fallback ke appsettings.json bawaan seperti biasa */ }

var builder = WebApplication.CreateBuilder(args);
// UseWindowsService() (2026-09-12) - HANYA benar2 aktif kalau proses ini
// SUNGGUHAN dimulai Service Control Manager (Process.GetCurrentProcess()
// parent != services.exe -> no-op otomatis) - 100% aman dipasang walau app
// tetap sering dijalankan cara lama (anak proses Launcher) atau `dotnet run`
// manual spt sepanjang sesi pengembangan ini, tidak mengubah perilaku
// apa pun di kedua skenario itu.
builder.Host.UseWindowsService();

// Simpan Data Protection Keys di folder stabil (2026-09-23, BUG NYATA - hapus
// tingkat & form POST lain mendadak "HTTP ERROR 400", halaman nyangkut sampai
// harus ditutup total & dibuka ulang) - tanpa ini, .NET bisa generate kunci
// BARU tiap proses restart (auto-update ApplyAndRestart, restart manual, dll).
// Begitu kunci berganti, SEMUA token antiforgery yang sudah terbit (termasuk
// yang sedang terbuka di WebView2 pengguna) langsung dianggap tidak valid ->
// validasi CSRF global (AutoValidateAntiforgeryTokenAttribute di bawah)
// menolaknya dgn HTTP 400 - utk SEMUA form POST tanpa kecuali, bukan cuma satu
// fitur. Kunci disimpan di folder yang SAMA dgn database (%LocalAppData%\DataMaster)
// supaya kunci, dan karenanya token yang sudah terbit, tetap valid lintas
// restart proses apa pun.
try
{
    var dpDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataMaster", "DataProtection-Keys");
    Directory.CreateDirectory(dpDir);
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dpDir));
}
catch { /* non-fatal - fallback ke kunci sementara bawaan kalau folder tidak bisa ditulis */ }

// Add services to the container.
// Session dipakai utk alur preview-import 2 langkah (persis pola PHP
// session()->set('preview_import_siswa', ...) di Siswa::previewImport() ->
// Siswa::showPreviewImport() -> Siswa::applyImport(), lihat 01-siswa-psb.md §3.13-15),
// DAN utk dev_preview_type (InstallTypeService, lihat 04-infra-auth-sync.md §5).
// TempData memakai session yang sama (bukan cookie) supaya flash message
// "message"/"error"/"warning" sekali-baca konsisten dgn semantik flashdata CI4.
builder.Services.AddControllersWithViews(options =>
    {
        // Validasi CSRF WAJIB di semua POST secara global - pola sama persis
        // csrf_field() CI4 yang otomatis divalidasi framework di setiap form.
        // Tiap <form method="post"> WAJIB menyertakan @Html.AntiForgeryToken().
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        // Login WAJIB secara global - pola sama semangat filter `login` yang
        // dipasang di HAMPIR semua rute PHP asli (lihat 04-infra-auth-sync.md §1.3).
        // AuthController diberi [AllowAnonymous] eksplisit supaya halaman
        // login/setup sendiri tidak ikut terkunci.
        options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));
    })
    .AddSessionStateTempDataProvider();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddScoped<LoginThrottleService>();
builder.Services.AddScoped<InstallTypeService>();

// Cookie auth - pengganti session-based Myth Auth PHP (lihat 04-infra-auth-sync.md
// §1.2, §6). RoleFilter PHP (`role:admin`) diganti [Authorize(Roles="admin")] per
// controller pendidikan; filter global di atas HANYA memaksa "sudah login", BUKAN
// grup tertentu - meniru pola PHP: filter `login` global + `role:admin` per-rute.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// SQLite tunggal, 1 file per instalasi - path default dev di App_Data/, TAPI
// Launcher (WPF) akan meng-override connection string ini saat menjalankan sbg
// child process supaya databasenya ditaruh di folder data per-PC yang benar
// (bukan di dalam folder aplikasi, supaya aman dari overwrite saat auto-update).
builder.Services.AddDbContext<DataMasterDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DataMaster")));
builder.Services.AddScoped<DocumentStorageService>();
builder.Services.AddScoped<PsbService>();
builder.Services.AddScoped<WaliKelasService>();
builder.Services.AddScoped<KepalaSekolahService>();
builder.Services.AddScoped<DatabaseBackupService>();
builder.Services.AddScoped<AppSettingsWriterService>();
builder.Services.AddHttpClient<HubApiRegistrationService>();

// Sinkronisasi Hub API (port SyncPush.php, lihat 04-infra-auth-sync.md §7) & Backup
// Awan terenkripsi (port BackupCloud.php, §8.2) - keduanya jalan sbg background
// service DI DALAM proses yang sama (adaptasi dari Windows Task Scheduler +
// proses CLI terpisah PHP asli, lihat komentar di masing2 HostedService).
builder.Services.AddHttpClient<HubApiSyncService>();
builder.Services.AddHttpClient<LulusanTkService>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<HubApiSyncHostedService>();
builder.Services.AddHostedService<BackupCloudHostedService>();
builder.Services.AddScoped<TrashPurgeService>();
builder.Services.AddHostedService<TrashPurgeHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Migrate otomatis saat start - identik semangat UPDATE.bat PHP asli
    // (php spark migrate dijalankan otomatis tiap update), tapi di sini cukup
    // panggil Migrate() krn EF Core migration sudah idempotent per-migrasi.
    var db = scope.ServiceProvider.GetRequiredService<DataMasterDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Auto-pulih dari HTTP 400 validasi CSRF (2026-09-24, BUG NYATA - laporan user:
// hapus Tingkat & form POST lain mendadak "This page isn't working, HTTP ERROR
// 400" mentah dari browser, TIDAK ADA cara pulih selain staf teknis manual hapus
// folder cache WebView2 di %LocalAppData%\DataMaster\webview2-data - jelas tidak
// masuk akal utk staf TU sekolah awam). Akar masalah (kunci Data Protection tidak
// persisten lintas restart, lihat komentar AddDataProtection di atas) SUDAH
// diperbaiki sejak v1.5.6, TAPI cookie yang SUDAH terlanjur rusak dari SEBELUM
// perbaikan itu tetap nyangkut selamanya di profil browser sampai dihapus manual -
// app ini SEKARANG membersihkan sendiri cookie yang bermasalah begitu 400 terjadi
// (bukan cuma sekali di sini, tapi utk KAPAN PUN validasi CSRF gagal ke depan,
// mis. kalau suatu saat Windows/AV mengosongkan folder DataProtection-Keys) lalu
// mengarahkan balik ke halaman asal - dari sudut pandang user awam, keliatannya
// cuma "kepencet, lalu halaman ke-refresh sendiri", tanpa perlu tahu apa pun soal
// cache/folder/CSRF sama sekali.
app.UseStatusCodePages(async statusCodeContext =>
{
    var http = statusCodeContext.HttpContext;
    if (http.Response.StatusCode != StatusCodes.Status400BadRequest || http.Response.HasStarted) return;

    foreach (var namaCookie in http.Request.Cookies.Keys)
    {
        if (namaCookie.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal) ||
            namaCookie.StartsWith(".AspNetCore.Cookies", StringComparison.Ordinal))
        {
            http.Response.Cookies.Delete(namaCookie, new CookieOptions { Path = "/" });
        }
    }

    var tujuan = http.Request.Headers.Referer.FirstOrDefault();
    if (string.IsNullOrEmpty(tujuan)) tujuan = "/";

    http.Response.ContentType = "text/html; charset=utf-8";
    await http.Response.WriteAsync($$"""
        <!DOCTYPE html><html><head><meta charset="utf-8">
        <title>Menyegarkan...</title></head>
        <body style="font-family:sans-serif;text-align:center;padding-top:15vh;color:#555">
        <p>Sesi perlu disegarkan sebentar, mengarahkan ulang...</p>
        <script>setTimeout(function () { location.href = {{JsonSerializer.Serialize(tujuan)}}; }, 600);</script>
        </body></html>
        """);
});

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Dipakai Launcher (WPF) utk polling "server sudah siap?" sebelum menampilkan
// WebView2 - endpoint Minimal API TIDAK ikut filter otorisasi global MVC di atas
// (filter itu cuma berlaku utk action controller), jadi sengaja tetap anonim.
app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
