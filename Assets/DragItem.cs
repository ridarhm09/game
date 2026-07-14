using UnityEngine;
using UnityEngine.EventSystems;

public class DragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Vector3 posisiAwal;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    [Header("Jenis Makanan (Susu/Pisang/Beef/Ayam/Wortel)")]
    public string jenisMakanan;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // Menambahkan CanvasGroup otomatis agar sistem drag tidak error
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Menyimpan posisi awal makanan sebelum diseret
        posisiAwal = rectTransform.anchoredPosition;
        
        // Mematikan raycast sementara agar mouse bisa mendeteksi objek di bawahnya (mulut kucing)
        canvasGroup.blocksRaycasts = false; 
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Membuat gambar buah mengikuti pergerakan jari atau mouse kamu
        rectTransform.anchoredPosition += eventData.delta / GetComponentInParent<Canvas>().scaleFactor;
    }

    // HANYA ADA SATU FUNGSI OnEndDrag DI SINI (Fungsi lama yang ganda sudah dihapus)
    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        
        // 1. Mengecek apakah makanan dilepas tepat di atas objek mulut kucing
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            string namaObjek = eventData.pointerCurrentRaycast.gameObject.name;

            if (namaObjek == "MulutKucing" || namaObjek == "Mulut_Kucing" || namaObjek == "Neko Cat 01")
            {
                ShopManager shop = FindFirstObjectByType<ShopManager>();
                if (shop != null)
                {
                    // Minta izin ke ShopManager untuk memotong stok makanan
                    shop.KucingMakan(jenisMakanan);
                    
                    // LANGSUNG HANCURKAN: Supaya makanan hilang dan gembok otomatis terbuka untuk makanan selanjutnya
                    Destroy(gameObject); 
                    return;
                }
            }
        }

        // 2. ALUR POU: Kalau dilepas di sembarang tempat (meleset), buah otomatis balik ke posisi awal karpet bunga
        rectTransform.anchoredPosition = posisiAwal; 
    }
}