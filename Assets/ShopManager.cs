using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("UI Global")]
    public TMP_Text teksPoin;
    public Transform posisiMunculMakanan;   

    [Header("Data Ekonomi")]
    public int totalPoin = 500;

    [Header("Stok Barang (Kulkas)")]
    public int stokSusu = 0;
    public int stokBanana = 0;
    public int stokBeef = 0;      
    public int stokChicken = 0;   
    public int stokCarrot = 0;

    [Header("Harga Barang di Toko")]
    public int hargaSusu = 10;
    public int hargaBanana = 15;
    public int hargaBeef = 25;
    public int hargaChicken = 20;
    public int hargaCarrot = 12;

    [Header("Teks Angka Stok di DAPUR (x0)")]
    public TMP_Text teksDapurSusu;
    public TMP_Text teksDapurBanana;
    public TMP_Text teksDapurBeef;
    public TMP_Text teksDapurChicken;
    public TMP_Text teksDapurCarrot;

    [Header("Panel Stok Makanan Dapur")]
    public GameObject panelStokMakanan; // Seret Panel_Stok_Makanan kamu ke sini

    [Header("SATU Prefab Utama Untuk Semua")]
    public GameObject prefabMakananTerbang; 

    [Header("Aset Gambar Asli dari Folder (food)")]
    public Sprite gambarSusu;
    public Sprite gambarBanana;
    public Sprite gambarBeef;
    public Sprite gambarChicken;
    public Sprite gambarCarrot;

    // Kunci agar tidak bisa memunculkan makanan baru sebelum yang di layar habis dimakan
    private bool sedangAdaMakananDiLayar = false;

    void Start()
    {
        UpdateSemuaUI();
        sedangAdaMakananDiLayar = false; // Memaksa sistem tahu kalau layar awal itu KOSONG
    }

    // Fungsi Beli di Toko: HANYA menambah stok, TIDAK memunculkan apa-apa di layar secara otomatis!
    public void BeliSusu() { if (totalPoin >= hargaSusu) { totalPoin -= hargaSusu; stokSusu++; UpdateSemuaUI(); } }
    public void BeliBanana() { if (totalPoin >= hargaBanana) { totalPoin -= hargaBanana; stokBanana++; UpdateSemuaUI(); } }
    public void BeliBeef() { if (totalPoin >= hargaBeef) { totalPoin -= hargaBeef; stokBeef++; UpdateSemuaUI(); } }
    public void BeliChicken() { if (totalPoin >= hargaChicken) { totalPoin -= hargaChicken; stokChicken++; UpdateSemuaUI(); } }
    public void BeliCarrot() { if (totalPoin >= hargaCarrot) { totalPoin -= hargaCarrot; stokCarrot++; UpdateSemuaUI(); } }

    // ALUR POU: Fungsi ini dipanggil saat TOMBOL STOK DAPUR diklik oleh user
    public void MunculkanMakananDariKulkas(string jenis)
    {
        // Jika masih ada makanan yang sedang melayang di layar, jangan kasih muncul baru dulu
        if (sedangAdaMakananDiLayar)
        {
            Debug.Log("Habiskan dulu makanan yang ada di layar!");
            return;
        }

        int stokSekarang = 0;
        Sprite gambarYangCocok = null;

        if (jenis == "Susu") { stokSekarang = stokSusu; gambarYangCocok = gambarSusu; }
        else if (jenis == "Pisang") { stokSekarang = stokBanana; gambarYangCocok = gambarBanana; }
        else if (jenis == "Beef") { stokSekarang = stokBeef; gambarYangCocok = gambarBeef; }
        else if (jenis == "Ayam") { stokSekarang = stokChicken; gambarYangCocok = gambarChicken; }
        else if (jenis == "Wortel") { stokSekarang = stokCarrot; gambarYangCocok = gambarCarrot; }

        // Jika stok di kulkas ada, lahirkan 1 buah di tengah layar!
        if (stokSekarang > 0 && prefabMakananTerbang != null)
        {
            GameObject makananBaru = Instantiate(prefabMakananTerbang, posisiMunculMakanan);
            makananBaru.transform.position = posisiMunculMakanan.position;

            Image komponenImage = makananBaru.GetComponent<Image>();
            if (komponenImage != null && gambarYangCocok != null)
            {
                komponenImage.sprite = gambarYangCocok;
            }

            DragItem scriptDrag = makananBaru.GetComponent<DragItem>();
            if (scriptDrag != null)
            {
                scriptDrag.jenisMakanan = jenis;
            }

            sedangAdaMakananDiLayar = true;

            // SAMA SEPERTI POU: Begitu item diklik, panel stok/kulkas langsung menutup otomatis
            if (panelStokMakanan != null)
            {
                panelStokMakanan.SetActive(false);
            }
        }
        else
        {
            Debug.Log("Stok " + jenis + " kosong! Beli dulu di toko.");
        }
    }

    // Fungsi dipanggil dari DragItem saat makanan MENYENTUH kucing dan hancur
// Fungsi dipanggil dari DragItem saat makanan MENYENTUH kucing dan hancur
    public bool KucingMakan(string jenis)
    {
        // PENTING: Kita cek dulu apakah pemotongan stoknya sukses atau tidak
        if (jenis == "Susu" && stokSusu > 0) { stokSusu--; sedangAdaMakananDiLayar = false; UpdateSemuaUI(); return true; }
        if (jenis == "Pisang" && stokBanana > 0) { stokBanana--; sedangAdaMakananDiLayar = false; UpdateSemuaUI(); return true; }
        if (jenis == "Beef" && stokBeef > 0) { stokBeef--; sedangAdaMakananDiLayar = false; UpdateSemuaUI(); return true; }
        if (jenis == "Ayam" && stokChicken > 0) { stokChicken--; sedangAdaMakananDiLayar = false; UpdateSemuaUI(); return true; }
        if (jenis == "Wortel" && stokCarrot > 0) { stokCarrot--; sedangAdaMakananDiLayar = false; UpdateSemuaUI(); return true; }
        
        // Jika tidak ada tipe yang cocok atau stok habis, paksa buka gembok agar makanan selanjutnya tidak macet
        sedangAdaMakananDiLayar = false;
        return false;
    }

    void UpdateSemuaUI()
    {
        if (teksPoin != null) teksPoin.text = "Poin: " + totalPoin;

        if (teksDapurSusu != null) teksDapurSusu.text = "x" + stokSusu;
        if (teksDapurBanana != null) teksDapurBanana.text = "x" + stokBanana;
        if (teksDapurBeef != null) teksDapurBeef.text = "x" + stokBeef;
        if (teksDapurChicken != null) teksDapurChicken.text = "x" + stokChicken;
        if (teksDapurCarrot != null) teksDapurCarrot.text = "x" + stokCarrot;
    }

    // Taruh fungsi baru ini di dalam script ShopManager.cs kamu
    public void BukaGembokMakanan()
    {
        sedangAdaMakananDiLayar = false;
        Debug.Log("Gembok makanan berhasil dibuka paksa!");
    }
}

