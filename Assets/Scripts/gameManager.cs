using UnityEngine;
using TMPro;

public class gameManager : MonoBehaviour
{
    public static gameManager Instance;

    public int poin = 500;       
    public TMP_Text teksPoin;    

    void Awake()
    {
        // Sistem pengaman agar Instance selalu aktif
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Sinkronisasi memori saat pertama kali game dinyalakan gres
        if (!PlayerPrefs.HasKey("TabunganPoinDonimal"))
        {
            PlayerPrefs.SetInt("TabunganPoinDonimal", poin);
            PlayerPrefs.Save();
        }
    }

    void Start()
    {
        // Ambil data dari brankas saat start
        poin = PlayerPrefs.GetInt("TabunganPoinDonimal", poin);
        UpdateTeksPoin();
    }

    // FUNGSI PENGANCAM REAL-TIME (DIPAKSA REFRESH SETIAP DETIK)
    void Update()
    {
        // 1. Selalu sedot data terbaru dari brankas laptop
        int poinTerbaruDiMemori = PlayerPrefs.GetInt("TabunganPoinDonimal", poin);
        
        // 2. Setel nilai internal script mengikuti brankas
        poin = poinTerbaruDiMemori;

        // 3. PAKSA GOYANG VISUAL TEKS: Jangan biarkan UI teks di layar diam membeku!
        if (teksPoin != null)
        {
            teksPoin.text = "poin : " + poin;
        }
    }

    public void UpdateTeksPoin()
    {
        if (teksPoin != null)
        {
            teksPoin.text = "poin : " + poin;
            teksPoin.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();
        }
    }

    // Fungsi pengurang poin saat belanja di toko Donimal
    public bool GunakanPoin(int jumlah)
    {
        poin = PlayerPrefs.GetInt("TabunganPoinDonimal", poin);

        if (poin >= jumlah)
        {
            poin -= jumlah;
            
            // Simpan sisa uang setelah belanja ke brankas
            PlayerPrefs.SetInt("TabunganPoinDonimal", poin);
            PlayerPrefs.Save();

            UpdateTeksPoin();
            return true; 
        }
        return false; 
    }
}