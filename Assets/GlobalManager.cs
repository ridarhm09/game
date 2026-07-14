using UnityEngine;
using UnityEngine.UI;

public class GlobalManager : MonoBehaviour
{
    [Header("Status Vital (Skala 0 - 100)")]
    [Range(0, 100)] public float makan = 100f;
    [Range(0, 100)] public float energi = 100f;
    [Range(0, 100)] public float sehat = 100f;
    [Range(0, 100)] public float senang = 100f;

    [Header("Komponen UI Slider (HUD Atas)")]
    public Slider sliderMakan;
    public Slider sliderEnergi;
    public Slider sliderSehat;
    public Slider sliderSenang;

    [Header("Animasi Karakter")]
    public Animator catAnimator;

    [Header("Sistem Navigasi Ruangan (Panel UI)")]
    // Masukkan panel ruangan sesuai urutan:
    // Index 0: Dashboard, Index 1: Tampilan Makan (Dapur), Index 2: Ruang Tidur, Index 3: Kamar Mandi (Lab)
    public GameObject[] ruanganGame; 
    private int indeksRuanganSekarang = 0; 

    // Timer internal untuk melacak durasi pengurangan status
    private float makanTimer;
    private float energiTimer;
    private float sehatTimer;
    private float senangTimer;

    void Start()
    {
        UpdateUI();
        UpdateTampilanRuangan();
    }

    void Update()
    {
        CalculateDecay();
        CheckCriticalCondition();
        UpdateUI();
    }

    /// <summary>
    /// Menghitung penurunan status berdasarkan waktu nyata (Real-time Decay sesuai GDD)
    /// </summary>
    void CalculateDecay()
    {
        // 1. Makan -= 1 setiap 10 detik
        makanTimer += Time.deltaTime;
        if (makanTimer >= 10f)
        {
            makan = Mathf.Max(0, makan - 1);
            makanTimer = 0f;
        }

        // 2. Energi -= 1 setiap 15 detik
        energiTimer += Time.deltaTime;
        if (energiTimer >= 15f)
        {
            energi = Mathf.Max(0, energi - 1);
            energiTimer = 0f;
        }

        // 3. Sehat -= 1 setiap 12 detik (Asumsi nilai penyusutan kesehatan harian)
        sehatTimer += Time.deltaTime;
        if (sehatTimer >= 12f)
        {
            sehat = Mathf.Max(0, sehat - 1);
            sehatTimer = 0f;
        }

        // 4. Senang -= 1 setiap 8 detik (Bosan/Fun)
        senangTimer += Time.deltaTime;
        if (senangTimer >= 8f)
        {
            senang = Mathf.Max(0, senang - 1);
            senangTimer = 0f;
        }
    }

    /// <summary>
    /// Pemicu animasi sedih jika ada status <= 30
    /// </summary>
    void CheckCriticalCondition()
    {
        if (catAnimator == null) return;

        if (makan <= 30f || energi <= 30f || sehat <= 30f || senang <= 30f)
        {
            catAnimator.SetBool("isSad", true);
        }
        else
        {
            catAnimator.SetBool("isSad", false);
        }
    }

    void UpdateUI()
    {
        if (sliderMakan != null) sliderMakan.value = makan;
        if (sliderEnergi != null) sliderEnergi.value = energi;
        if (sliderSehat != null) sliderSehat.value = sehat;
        if (sliderSenang != null) sliderSenang.value = senang;
    }

    // =======================================================
    // MEKANIK NAVIGASI RUANGAN (Tombol Panah)
    // =======================================================

    public void KeRuanganSelanjutnya()
    {
        indeksRuanganSekarang++;
        if (indeksRuanganSekarang >= ruanganGame.Length)
        {
            indeksRuanganSekarang = 0; 
        }
        UpdateTampilanRuangan();
    }

    public void KeRuanganSebelumnya()
    {
        indeksRuanganSekarang--;
        if (indeksRuanganSekarang < 0)
        {
            indeksRuanganSekarang = ruanganGame.Length - 1; 
        }
        UpdateTampilanRuangan();
    }

    private void UpdateTampilanRuangan()
    {
        for (int i = 0; i < ruanganGame.Length; i++)
        {
            if (ruanganGame[i] != null)
            {
                ruanganGame[i].SetActive(i == indeksRuanganSekarang);
            }
        }
    }
}