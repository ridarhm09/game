using System.Collections;
using TMPro;
using UnityEngine;

public class PopupLetterSlot : MonoBehaviour
{
    [Header("Data Huruf")]
    [Tooltip("Isi huruf kecil, misalnya t, a, atau s.")]
    public string letter;

    [Header("Komponen")]
    public RectTransform letterRect;
    public CanvasGroup canvasGroup;

    [Tooltip(
        "Boleh kosong jika huruf menggunakan gambar, bukan TMP Text."
    )]
    public TMP_Text letterText;

    [Header("Animasi Popup")]
    public float popupDuration = 0.3f;
    public float startScale = 0f;
    public float overshootScale = 1.2f;

    private bool isRevealed;
    private Coroutine popupCoroutine;

    private void Awake()
    {
        AutoFillComponents();
        ResetSlot();
    }

    private void AutoFillComponents()
    {
        if (letterRect == null)
        {
            letterRect =
                GetComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup =
                GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup =
                gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void Setup(string newLetter)
    {
        letter =
            NormalizeLetter(newLetter);

        if (letterText != null)
        {
            letterText.text =
                letter.ToUpper();
        }

        ResetSlot();
    }

    public void ResetSlot()
    {
        AutoFillComponents();

        isRevealed = false;

        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
            popupCoroutine = null;
        }

        if (letterRect != null)
        {
            letterRect.localScale =
                Vector3.one * startScale;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public bool Reveal()
    {
        if (isRevealed)
        {
            return false;
        }

        isRevealed = true;

        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
        }

        popupCoroutine =
            StartCoroutine(PopupAnimation());

        return true;
    }

    public bool IsRevealed()
    {
        return isRevealed;
    }

    public string GetLetter()
    {
        return NormalizeLetter(letter);
    }
    public char targetLetter { get { return string.IsNullOrEmpty(letter) ? ' ' : letter[0]; } }
    public bool IsFilled() { return IsRevealed(); }
    public void FillSlot() { Reveal(); }

    private IEnumerator PopupAnimation()
    {
        AutoFillComponents();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        float timer = 0f;

        while (timer < popupDuration)
        {
            timer += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    timer / popupDuration
                );

            float scale;

            if (progress < 0.7f)
            {
                scale = Mathf.Lerp(
                    startScale,
                    overshootScale,
                    progress / 0.7f
                );
            }
            else
            {
                scale = Mathf.Lerp(
                    overshootScale,
                    1f,
                    (progress - 0.7f) / 0.3f
                );
            }

            if (letterRect != null)
            {
                letterRect.localScale =
                    Vector3.one * scale;
            }

            yield return null;
        }

        if (letterRect != null)
        {
            letterRect.localScale =
                Vector3.one;
        }

        popupCoroutine = null;
    }

    private string NormalizeLetter(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string result =
            value.ToLower().Trim();

        if (result.StartsWith("huruf:"))
        {
            result =
                result.Substring("huruf:".Length);
        }

        return result.Trim();
    }
}
