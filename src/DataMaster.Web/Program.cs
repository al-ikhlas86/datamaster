using System.Text.Json;
using System.Text.Json.Nodes;
using DataMaster.Data;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

var builder = WebApplication.CreateBuilder(args);

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

// Sinkronisasi Hub API (port SyncPush.php, lihat 04-infra-auth-sync.md §7) & Backup
// Awan terenkripsi (port BackupCloud.php, §8.2) - keduanya jalan sbg background
// service DI DALAM proses yang sama (adaptasi dari Windows Task Scheduler +
// proses CLI terpisah PHP asli, lihat komentar di masing2 HostedService).
builder.Services.AddHttpClient<HubApiSyncService>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<HubApiSyncHostedService>();
builder.Services.AddHostedService<BackupCloudHostedService>();

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
