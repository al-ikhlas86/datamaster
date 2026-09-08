using DataMaster.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// SQLite tunggal, 1 file per instalasi - path default dev di App_Data/, TAPI
// Launcher (WPF) akan meng-override connection string ini saat menjalankan sbg
// child process supaya databasenya ditaruh di folder data per-PC yang benar
// (bukan di dalam folder aplikasi, supaya aman dari overwrite saat auto-update).
builder.Services.AddDbContext<DataMasterDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DataMaster")));

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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
