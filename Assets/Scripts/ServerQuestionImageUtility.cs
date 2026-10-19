using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Helper untuk menampilkan Texture2D hasil download dari server
/// ke UI soal. Mendukung RawImage lama maupun Image (Sprite) baru.
/// </summary>
public static class ServerQuestionImageUtility
{
    public static bool ApplyTexture(GameObject target, Texture2D texture, string logPrefix = "[QuestionImage]")
    {
        if (target == null)
        {
            Debug.LogError(logPrefix + " Target GameObject null.");
            return false;
        }

        if (texture == null)
        {
            Debug.LogError(logPrefix + " Texture null.");
            return false;
        }

        RawImage rawImage = target.GetComponent<RawImage>();
        if (rawImage == null)
            rawImage = target.GetComponentInChildren<RawImage>(true);

        if (rawImage != null)
        {
            rawImage.texture = texture;
            rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            // Pertahankan ukuran, skala, dan anchor yang sudah diatur pada template.
            return true;
        }

        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.GetComponentInChildren<Image>(true);

        if (image != null)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
            image.sprite = sprite;
            image.preserveAspect = true;
            return true;
        }

        RawImage generatedImage = target.AddComponent<RawImage>();
        generatedImage.raycastTarget = false;
        RectTransform rectTransform = generatedImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        generatedImage.texture = texture;
        generatedImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        Debug.Log("[QuestionImage] RawImage otomatis dibuat pada " + target.name);
        return true;
    }


    /// <summary>
    /// Terapkan Sprite lokal (dari LocalQuestionImageCatalog) ke UI soal.
    /// Digunakan saat offline mode aktif.
    /// </summary>
    public static bool ApplySprite(GameObject target, Sprite sprite, string logPrefix = "[QuestionImage]")
    {
        if (target == null)
        {
            Debug.LogError(logPrefix + " Target GameObject null.");
            return false;
        }
        if (sprite == null)
        {
            Debug.LogWarning(logPrefix + " Sprite null, tampilan dilewati.");
            return false;
        }

        // Prioritas 1: Image component
        Image image = target.GetComponent<Image>();
        if (image == null) image = target.GetComponentInChildren<Image>(true);
        if (image != null)
        {
            image.sprite = sprite;
            image.preserveAspect = true;
            return true;
        }

        // Prioritas 2: RawImage — konversi Sprite ke Texture2D
        RawImage rawImage = target.GetComponent<RawImage>();
        if (rawImage == null) rawImage = target.GetComponentInChildren<RawImage>(true);
        if (rawImage != null)
        {
            rawImage.texture = sprite.texture;
            rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            return true;
        }

        // Fallback: tambah Image component baru
        Image generatedImage = target.AddComponent<Image>();
        generatedImage.sprite = sprite;
        generatedImage.preserveAspect = true;
        generatedImage.raycastTarget = false;
        Debug.Log(logPrefix + " Image otomatis dibuat pada " + target.name);
        return true;
    }
}
