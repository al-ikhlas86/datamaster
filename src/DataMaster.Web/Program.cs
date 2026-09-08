using DataMaster.Data;
using DataMaster.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

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

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
