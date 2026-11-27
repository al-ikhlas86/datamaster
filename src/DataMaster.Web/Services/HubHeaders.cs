namespace DataMaster.Web.Services;

// Identitas aplikasi untuk SETIAP permintaan ke Hub API: versi (X-App-Version, untuk Admin IT melihat dari jauh unit mana yang sudah ter-update) dan pengenal
// PC (X-Device-Uid, untuk Blokir perangkat). Dulu header ini hanya dipasang pada pengiriman data (POST), yang dilewati bila data tidak berubah, sehingga
// unit yang datanya stabil tampil "versi belum dilaporkan" walau sudah terbaru. Semua pemanggil Hub memakai ini supaya tidak ada yang terlewat.
public static class HubHeaders
{
    public static void Terapkan(HttpRequestMessage req)
    {
        if (AppVersionInfo.Resmi is { } versiApp) req.Headers.TryAddWithoutValidation("X-App-Version", versiApp);
        if (DeviceIdentity.Uid is { } uidPc) req.Headers.TryAddWithoutValidation("X-Device-Uid", uidPc);
    }
}
