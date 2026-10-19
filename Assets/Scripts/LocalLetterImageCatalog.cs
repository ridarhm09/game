// Assets/Scripts/LocalLetterImageCatalog.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DONIMAL/Local Letter Image Catalog")]
public class LocalLetterImageCatalog : ScriptableObject
{
    [Serializable]
    public class LetterEntry
    {
        [Tooltip("Huruf tunggal, contoh: A, B, 1, 2")] public string letter; // case‑insensitive
        public Sprite sprite;                                            // gambar huruf
    }

    public List<LetterEntry> entries = new List<LetterEntry>();

    // -----------------------------------------------------------------
    // LOOK‑UP
    // -----------------------------------------------------------------
    private Dictionary<string, Sprite> _lookup;

    private void OnEnable()
    {
        _lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.letter) || e.sprite == null) continue;
            _lookup[e.letter.Trim()] = e.sprite;
        }
    }

    public Sprite GetSprite(string letter)
    {
        if (string.IsNullOrWhiteSpace(letter)) return null;
        _lookup.TryGetValue(letter.Trim(), out Sprite s);
        return s;
    }
}
