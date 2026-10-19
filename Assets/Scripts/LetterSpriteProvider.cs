using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads all letter/digit sprites from Resources/LearningLetters (including subfolders)
/// and provides fast case-insensitive lookup via GetSprite(letter).
/// Attach this MonoBehaviour to any persistent GameObject in the scene.
/// </summary>
public class LetterSpriteProvider : MonoBehaviour
{
    // key = letter name (case-insensitive), value = sprite
    private readonly Dictionary<string, Sprite> _sprites =
        new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        // Load from known sub-paths explicitly so Unity finds them even if
        // the folder contains sub-directories (Resources.LoadAll recurses but
        // the key used is the full relative path, not just the filename).
        string[] subPaths = { "LearningLetters/huruf", "LearningLetters/angka", "LearningLetters" };

        foreach (string path in subPaths)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(path);
            foreach (Sprite spr in loaded)
            {
                if (spr == null) continue;
                string key = spr.name.Trim();
                if (!string.IsNullOrEmpty(key) && !_sprites.ContainsKey(key))
                    _sprites[key] = spr;
            }
        }

        if (_sprites.Count == 0)
            Debug.LogWarning("[LetterSpriteProvider] No sprites found. " +
                             "Make sure PNGs are in Assets/Resources/LearningLetters/huruf/ " +
                             "and their Texture Type is set to 'Sprite (2D and UI)'.");
        else
            Debug.Log("[LetterSpriteProvider] Loaded " + _sprites.Count + " letter/digit sprites.");
    }

    /// <summary>Returns the sprite for the given letter/digit, or null if not found.</summary>
    public Sprite GetSprite(string letter)
    {
        if (string.IsNullOrWhiteSpace(letter)) return null;
        _sprites.TryGetValue(letter.Trim(), out Sprite s);
        return s;
    }
}
