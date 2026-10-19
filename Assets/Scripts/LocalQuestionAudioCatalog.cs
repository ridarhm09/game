using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DONIMAL/Local Question Audio Catalog")]
public class LocalQuestionAudioCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Catatan soal. Untuk suara hewan lokal, gunakan label Tebak Suara diikuti nama hewan.")]
        public string label;
        [Tooltip("ID soal persis dari server, bukan nomor urutan audio.")]
        public string questionId;
        public AudioClip kira;
        public AudioClip eyra;
        public AudioClip pasa;
    }

    [Serializable]
    public class Subgame
    {
        public string name;
        public List<Entry> questions = new List<Entry>();
    }

    [Serializable]
    public class Category
    {
        public string name;
        public List<Subgame> subgames = new List<Subgame>();
    }

    public List<Category> categories = new List<Category>();

#if UNITY_EDITOR
    // Gunakan objek yang sedang dibuka Unity agar isian Inspector tetap terjaga.
    public void ImportHitungQuestionIds(List<GameQuestionData> data)
    {
        ImportQuestionIds(data, "Angka", "Hitung Angka", "[HitungQuestion]");
    }

    public void ImportWarnaQuestionIds(List<GameQuestionData> data)
    {
        ImportQuestionIds(data, "Warna", "Tebak Warna", "[TebakWarna]");
    }

    public void ImportSuaraQuestionIds(List<GameQuestionData> data)
    {
        ImportQuestionIds(data, "Hewan", "Tebak Suara", "[SuaraQuestion]");
    }

    public void ImportSusunQuestionIds(List<GameQuestionData> data)
    {
        ImportQuestionIds(data, "Angka", "Susun Angka", "[SusunQuestion]");
    }

    private void ImportQuestionIds(List<GameQuestionData> data, string categoryName,
        string subgameName, string logPrefix)
    {
        if (data == null || !data.Exists(question => question != null &&
            !string.IsNullOrWhiteSpace(question.id) && !ContainsQuestion(question.id))) return;
        UnityEditor.Undo.RecordObject(this, "Import ID " + subgameName);
        if (categories == null) categories = new List<Category>();
        Category category = categories.Find(item => item != null && item.name == categoryName);
        if (category == null)
        {
            category = new Category { name = categoryName };
            categories.Add(category);
        }
        if (category.subgames == null) category.subgames = new List<Subgame>();
        Subgame subgame = category.subgames.Find(item => item != null && item.name == subgameName);
        if (subgame == null)
        {
            subgame = new Subgame { name = subgameName };
            category.subgames.Add(subgame);
        }
        if (subgame.questions == null) subgame.questions = new List<Entry>();
        int added = 0;
        foreach (GameQuestionData question in data)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.id) || ContainsQuestion(question.id)) continue;
            subgame.questions.Add(new Entry
            {
                label = subgameName + " " + question.correct_answer,
                questionId = question.id.Trim()
            });
            added++;
        }
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
        Debug.Log(logPrefix + " Import katalog selesai: " + added +
                  " ID baru ditambahkan ke " + categoryName + " > " + subgameName +
                  ". Pasangan audio lama dipertahankan.");
    }
#endif

    // Pertahankan kompatibilitas dengan katalog lama yang belum dikelompokkan.
    [HideInInspector] public List<Entry> questions = new List<Entry>();

    private IEnumerable<Entry> GetEntries()
    {
        if (categories != null)
            foreach (Category category in categories)
            {
                if (category == null || category.subgames == null) continue;
                foreach (Subgame subgame in category.subgames)
                {
                    if (subgame == null || subgame.questions == null) continue;
                    foreach (Entry entry in subgame.questions) yield return entry;
                }
            }
        if (questions != null)
            foreach (Entry entry in questions) yield return entry;
    }

    public bool ContainsQuestion(string questionId)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return false;
        foreach (Entry entry in GetEntries())
            if (entry != null && !string.IsNullOrWhiteSpace(entry.questionId) &&
                string.Equals(entry.questionId.Trim(), questionId.Trim(), StringComparison.Ordinal)) return true;
        return false;
    }

    public AudioClip GetClip(string questionId, string character)
    {
        if (string.IsNullOrWhiteSpace(questionId)) return null;
        foreach (Entry entry in GetEntries())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.questionId) ||
                !string.Equals(entry.questionId.Trim(), questionId.Trim(), StringComparison.Ordinal)) continue;
            switch (character)
            {
                case "kira": return entry.kira;
                case "eyra": return entry.eyra;
                case "pasa": return entry.pasa;
                default: return null;
            }
        }
        return null;
    }

    public AudioClip GetClipByLabel(string label, string character)
    {
        foreach (Entry entry in GetEntries())
        {
            if (entry == null || !string.Equals(entry.label?.Trim(), label?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            AudioClip clip = character == "kira" ? entry.kira : character == "eyra" ? entry.eyra : character == "pasa" ? entry.pasa : null;
            if (clip != null) return clip;
        }
        return null;
    }

    public AudioClip GetAnimalQuestionClip(string animal, string character)
    {
        string label = "Tebak Suara " + RfidAnswerUtility.Normalize("hewan", animal);
        foreach (Entry entry in GetEntries())
        {
            if (entry != null && string.Equals(entry.label, label, StringComparison.OrdinalIgnoreCase))
                return GetClip(entry.questionId, character);
        }
        return null;
    }

    private void OnValidate()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Entry entry in GetEntries())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.questionId)) continue;
            if (!ids.Add(entry.questionId.Trim()))
                 Debug.LogWarning("[QuestionAudio] ID soal duplikat: " + entry.questionId +
                                 ". Gunakan satu baris untuk setiap ID soal.", this);
        }
    }
}
