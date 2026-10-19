using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Katalog gambar soal lokal untuk mode offline.
/// Simpan referensi Sprite gambar soal dan jawaban di sini agar game bisa
/// berjalan tanpa koneksi server.
/// Buat asset via menu: DONIMAL > Local Question Image Catalog
/// </summary>
[CreateAssetMenu(menuName = "DONIMAL/Local Question Image Catalog")]
public class LocalQuestionImageCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Keterangan soal, contoh: 'anjing', 'merah', '5', 'A'.")]
        public string label;

        [Tooltip("ID soal dari server (opsional, untuk kompatibilitas saat online lalu cache lokal).")]
        public string questionId;

        [Tooltip("Gambar yang ditampilkan sebagai soal (pertanyaan).")]
        public Sprite questionImage;

        [Tooltip("Gambar jawaban / gambar tabel belajar yang ditampilkan setelah soal.")]
        public Sprite answerImage;
    }

    [Serializable]
    public class Subgame
    {
        public string name;
        public List<Entry> entries = new List<Entry>();
    }

    [Serializable]
    public class Category
    {
        public string name;
        public List<Subgame> subgames = new List<Subgame>();
    }

    public List<Category> categories = new List<Category>();

    // ------------------------------------------------
    // LOOK UP METHODS
    // ------------------------------------------------

    private IEnumerable<Entry> GetAllEntries()
    {
        if (categories == null) yield break;
        foreach (Category cat in categories)
        {
            if (cat == null || cat.subgames == null) continue;
            foreach (Subgame sub in cat.subgames)
            {
                if (sub == null || sub.entries == null) continue;
                foreach (Entry entry in sub.entries) yield return entry;
            }
        }
    }

    private IEnumerable<Entry> GetEntriesForCategory(string categoryName, string subgameName = null)
    {
        if (categories == null) yield break;
        foreach (Category cat in categories)
        {
            if (cat == null || !string.Equals(cat.name, categoryName, StringComparison.OrdinalIgnoreCase)) continue;
            if (cat.subgames == null) continue;
            foreach (Subgame sub in cat.subgames)
            {
                if (sub == null || sub.entries == null) continue;
                if (!string.IsNullOrEmpty(subgameName) &&
                    !string.Equals(sub.name, subgameName, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (Entry entry in sub.entries) yield return entry;
            }
        }
    }

    /// <summary>
    /// Cari gambar soal berdasarkan label (misal: "anjing", "5", "A").
    /// </summary>
    public Sprite GetQuestionImage(string label, string categoryName = null, string subgameName = null)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        IEnumerable<Entry> pool = string.IsNullOrEmpty(categoryName)
            ? GetAllEntries()
            : GetEntriesForCategory(categoryName, subgameName);
        foreach (Entry entry in pool)
        {
            if (entry != null && string.Equals(entry.label?.Trim(), label.Trim(), StringComparison.OrdinalIgnoreCase))
                return entry.questionImage;
        }
        return null;
    }

    /// <summary>
    /// Cari gambar jawaban berdasarkan label (misal: "anjing", "5", "A").
    /// </summary>
    public Sprite GetAnswerImage(string label, string categoryName = null, string subgameName = null)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        IEnumerable<Entry> pool = string.IsNullOrEmpty(categoryName)
            ? GetAllEntries()
            : GetEntriesForCategory(categoryName, subgameName);
        foreach (Entry entry in pool)
        {
            if (entry != null && string.Equals(entry.label?.Trim(), label.Trim(), StringComparison.OrdinalIgnoreCase))
                return entry.answerImage;
        }
        return null;
    }

    /// <summary>
    /// Cari gambar soal berdasarkan question ID dari server.
    /// </summary>
    public Sprite GetQuestionImageById(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return null;
        foreach (Entry entry in GetAllEntries())
        {
            if (entry != null && string.Equals(entry.questionId?.Trim(), questionId.Trim(), StringComparison.Ordinal))
                return entry.questionImage;
        }
        return null;
    }

    /// <summary>
    /// Cari gambar jawaban berdasarkan question ID dari server.
    /// </summary>
    public Sprite GetAnswerImageById(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return null;
        foreach (Entry entry in GetAllEntries())
        {
            if (entry != null && string.Equals(entry.questionId?.Trim(), questionId.Trim(), StringComparison.Ordinal))
                return entry.answerImage;
        }
        return null;
    }

    /// <summary>
    /// Ambil semua label yang terdaftar dalam kategori/subgame tertentu.
    /// Berguna bagi controller untuk memilih soal secara acak saat offline.
    /// </summary>
    public List<string> GetLabels(string categoryName = null, string subgameName = null)
    {
        var result = new List<string>();
        IEnumerable<Entry> pool = string.IsNullOrEmpty(categoryName)
            ? GetAllEntries()
            : GetEntriesForCategory(categoryName, subgameName);
        foreach (Entry entry in pool)
            if (entry != null && !string.IsNullOrWhiteSpace(entry.label))
                result.Add(entry.label.Trim());
        return result;
    }

    private void OnValidate()
    {
        if (categories == null) return;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Entry entry in GetAllEntries())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.questionId)) continue;
            if (!ids.Add(entry.questionId.Trim()))
                Debug.LogWarning("[QuestionImage] ID soal duplikat: " + entry.questionId, this);
        }
    }
}
