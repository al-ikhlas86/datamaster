namespace DataMaster.Web.Services;

// Port dari pola uploadDokumen()/hapusDokumen() PHP (Siswa.php/CalonSiswa.php) -
// file disimpan DI LUAR wwwroot (analog WRITEPATH/uploads/dokumen/ PHP, bukan
// FCPATH/webroot) supaya TIDAK bisa diakses langsung lewat URL statis - satu-satunya
// jalan baca adalah endpoint terproteksi login (SiswaController.Dokumen), persis
// 01-siswa-psb.md §3.1.
public class DocumentStorageService(IWebHostEnvironment env)
{
    private readonly string _root = Path.Combine(env.ContentRootPath, "App_Data", "uploads", "dokumen");

    public string RootPath => _root;

    private static readonly string[] AllowedExtensions = [".pdf", ".jpg", ".jpeg", ".png"];
    private static readonly string[] AllowedContentTypes = ["application/pdf", "image/jpeg", "image/png"];
    private const long MaxSizeBytes = 2 * 1024 * 1024; // 2MB, lihat 01-siswa-psb.md §3.4 (max_size[field,2048])

    public bool IsValidDocument(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return file.Length > 0
            && file.Length <= MaxSizeBytes
            && AllowedExtensions.Contains(ext)
            && AllowedContentTypes.Contains(file.ContentType);
    }

    /// <summary>Simpan file dengan nama random, kembalikan nama file yang disimpan (bukan path penuh).</summary>
    public async Task<string> SaveAsync(IFormFile file)
    {
        Directory.CreateDirectory(_root);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var namaFile = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(_root, namaFile);
        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);
        return namaFile;
    }

    public void Delete(string? namaFile)
    {
        if (string.IsNullOrWhiteSpace(namaFile)) return;
        var fullPath = Path.Combine(_root, namaFile);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    public string? GetFullPath(string? namaFile)
    {
        if (string.IsNullOrWhiteSpace(namaFile)) return null;
        var fullPath = Path.Combine(_root, namaFile);
        return File.Exists(fullPath) ? fullPath : null;
    }

    public static string GetContentType(string namaFile) => Path.GetExtension(namaFile).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        _ => "application/octet-stream",
    };
}
