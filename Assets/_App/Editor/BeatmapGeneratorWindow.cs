using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class BeatmapGeneratorWindow : EditorWindow
{
    private AudioClip selectedAudioClip;
    private float bpm = 120f; // 改为可调节的BPM
    private int totalBeats = 100;
    private int lanes = 3;
    private int offsetMs = 0;
    private string patternType = "Basic";
    private string[] patternTypes = new string[] {
        "Basic",
        "Alternating",
        "Random",
        "Custom Pattern",
        "Dance Pattern",
        "Fast Stream"
    };
    private string customPattern = "0,1,2,0,1,2";

    // BPM 推荐预设
    private readonly float[] bpmPresets = { 60f, 80f, 100f, 120f, 140f, 150f, 160f, 180f };

    [MenuItem("Tools/Beatmap Generator")]
    public static void ShowWindow()
    {
        GetWindow<BeatmapGeneratorWindow>("Beatmap Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("BEATMAP GENERATOR", EditorStyles.boldLabel);

        selectedAudioClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", selectedAudioClip, typeof(AudioClip), false);

        EditorGUILayout.Space();
        GUILayout.Label("节奏设置", EditorStyles.boldLabel);

        // BPM 输入字段
        bpm = EditorGUILayout.FloatField("BPM", bpm);
        bpm = Mathf.Clamp(bpm, 30f, 300f); // 限制在合理范围内

        // BPM 预设按钮
        GUILayout.Label("BPM 预设:", EditorStyles.miniLabel);
        GUILayout.BeginHorizontal();
        for (int i = 0; i < bpmPresets.Length; i++)
        {
            if (GUILayout.Button(bpmPresets[i].ToString(), GUILayout.Width(40)))
            {
                bpm = bpmPresets[i];
            }
        }
        GUILayout.EndHorizontal();

        // BPM 说明
        string bpmDescription = GetBPMDescription(bpm);
        EditorGUILayout.HelpBox(bpmDescription, MessageType.Info);

        totalBeats = EditorGUILayout.IntSlider("总拍数", totalBeats, 10, 500);
        lanes = EditorGUILayout.IntSlider("轨道数", lanes, 1, 5);
        offsetMs = EditorGUILayout.IntField("时间偏移 (毫秒)", offsetMs);

        EditorGUILayout.Space();
        GUILayout.Label("节奏模式", EditorStyles.boldLabel);

        patternType = patternTypes[EditorGUILayout.Popup("模式类型", System.Array.IndexOf(patternTypes, patternType), patternTypes)];

        if (patternType == "Custom Pattern")
        {
            customPattern = EditorGUILayout.TextField("自定义模式", customPattern);
            EditorGUILayout.HelpBox("输入逗号分隔的轨道编号 (例如: 0,1,2,0,1,2)", MessageType.Info);
        }

        EditorGUILayout.Space();

        // 按钮区域
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("生成谱面JSON"))
        {
            string json = GenerateBeatmapJson();
            EditorGUIUtility.systemCopyBuffer = json;
            Debug.Log($"BPM {bpm} 谱面JSON已生成并复制到剪贴板!");
            ShowNotification(new GUIContent("JSON已复制!"));
        }

        if (GUILayout.Button("应用到选中的歌曲"))
        {
            ApplyToSelectedSong();
        }
        GUILayout.EndHorizontal();

        // 预览区域
        EditorGUILayout.Space();
        GUILayout.Label("谱面预览", EditorStyles.boldLabel);
        string previewJson = GenerateBeatmapJson();
        EditorGUILayout.TextArea(previewJson, GUILayout.Height(120));

        // 显示统计信息 - 使用简单的方法避免类冲突
        int noteCount = CountNotesInJson(previewJson);
        float totalTime = CalculateTotalTime(previewJson);
        EditorGUILayout.HelpBox(
            $"谱面统计:\n" +
            $"• 音符数量: {noteCount}\n" +
            $"• 总时长: {totalTime:F1} 秒\n" +
            $"• 每分钟节拍: {bpm} BPM",
            MessageType.None
        );
    }

    private string GetBPMDescription(float bpmValue)
    {
        if (bpmValue < 70) return "慢速 - 适合抒情/放松音乐";
        if (bpmValue < 100) return "中速 - 适合流行音乐";
        if (bpmValue < 130) return "快步 - 适合舞曲";
        if (bpmValue < 160) return "快速 - 适合电子音乐";
        return "极速 - 适合硬核/高速音乐";
    }

    private string GenerateBeatmapJson()
    {
        float beatIntervalMs = 60000f / bpm; // 每拍多少毫秒

        // 使用动态对象构建JSON
        var chartData = new Dictionary<string, object>();
        chartData["Lanes"] = lanes;
        chartData["OffsetMs"] = offsetMs;

        List<Dictionary<string, object>> notesList = new List<Dictionary<string, object>>();

        // 根据模式生成音符
        switch (patternType)
        {
            case "Dance Pattern":
                GenerateDancePattern(notesList, beatIntervalMs);
                break;
            case "Fast Stream":
                GenerateFastStreamPattern(notesList, beatIntervalMs);
                break;
            default:
                GenerateStandardPattern(notesList, beatIntervalMs);
                break;
        }

        chartData["Notes"] = notesList.ToArray();

        // 手动构建JSON字符串
        return BuildJsonString(chartData);
    }

    private void GenerateStandardPattern(List<Dictionary<string, object>> notes, float beatIntervalMs)
    {
        int[] pattern = null;

        if (patternType == "Custom Pattern")
        {
            string[] patternStrs = customPattern.Split(',');
            pattern = new int[patternStrs.Length];
            for (int i = 0; i < patternStrs.Length; i++)
            {
                int.TryParse(patternStrs[i].Trim(), out pattern[i]);
            }
        }

        for (int i = 0; i < totalBeats; i++)
        {
            int lane = 0;

            switch (patternType)
            {
                case "Basic":
                    lane = i % lanes;
                    break;
                case "Alternating":
                    lane = (i % 2 == 0) ? 0 : (lanes > 1 ? 1 : 0);
                    break;
                case "Random":
                    lane = Random.Range(0, lanes);
                    break;
                case "Custom Pattern":
                    if (pattern != null && pattern.Length > 0)
                        lane = pattern[i % pattern.Length];
                    else
                        lane = i % lanes;
                    break;
            }

            var note = new Dictionary<string, object>();
            note["T"] = (int)(i * beatIntervalMs);
            note["Lane"] = Mathf.Clamp(lane, 0, lanes - 1);
            notes.Add(note);
        }
    }

    private void GenerateDancePattern(List<Dictionary<string, object>> notes, float beatIntervalMs)
    {
        string dancePattern = "0,1,2,1,0,2,1,0";
        string[] patternStrs = dancePattern.Split(',');
        int[] pattern = new int[patternStrs.Length];
        for (int i = 0; i < patternStrs.Length; i++)
        {
            int.TryParse(patternStrs[i].Trim(), out pattern[i]);
        }

        for (int i = 0; i < totalBeats; i++)
        {
            int lane = pattern[i % pattern.Length];
            var note = new Dictionary<string, object>();
            note["T"] = (int)(i * beatIntervalMs);
            note["Lane"] = Mathf.Clamp(lane, 0, lanes - 1);
            notes.Add(note);
        }
    }

    private void GenerateFastStreamPattern(List<Dictionary<string, object>> notes, float beatIntervalMs)
    {
        string fastStreamPattern = "0,1,2,0,1,2,0,1,2,0,1,2";
        string[] patternStrs = fastStreamPattern.Split(',');
        int[] pattern = new int[patternStrs.Length];
        for (int i = 0; i < patternStrs.Length; i++)
        {
            int.TryParse(patternStrs[i].Trim(), out pattern[i]);
        }

        // 每拍生成3个音符
        int totalNotes = totalBeats * 3;
        for (int i = 0; i < totalNotes; i++)
        {
            int lane = pattern[i % pattern.Length];
            float time = (i * beatIntervalMs) / 3f;

            var note = new Dictionary<string, object>();
            note["T"] = (int)time;
            note["Lane"] = Mathf.Clamp(lane, 0, lanes - 1);
            notes.Add(note);
        }
    }

    private string BuildJsonString(Dictionary<string, object> data)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("{");

        bool first = true;
        foreach (var kvp in data)
        {
            if (!first) sb.Append(",");
            first = false;

            sb.Append($"\"{kvp.Key}\":");

            if (kvp.Value is int || kvp.Value is float)
            {
                sb.Append(kvp.Value);
            }
            else if (kvp.Value is Dictionary<string, object>[] notesArray)
            {
                sb.Append("[");
                bool firstNote = true;
                foreach (var note in notesArray)
                {
                    if (!firstNote) sb.Append(",");
                    firstNote = false;

                    sb.Append("{");
                    bool firstProp = true;
                    foreach (var noteProp in note)
                    {
                        if (!firstProp) sb.Append(",");
                        firstProp = false;
                        sb.Append($"\"{noteProp.Key}\":{noteProp.Value}");
                    }
                    sb.Append("}");
                }
                sb.Append("]");
            }
            else
            {
                sb.Append($"\"{kvp.Value}\"");
            }
        }

        sb.Append("}");
        return sb.ToString();
    }

    private void ApplyToSelectedSong()
    {
        // 找到 SongDatabaseSO
        string[] guids = AssetDatabase.FindAssets("t:SongDatabaseSO");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "找不到 SongDatabaseSO", "确定");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        SongDatabaseSO database = AssetDatabase.LoadAssetAtPath<SongDatabaseSO>(path);

        if (database == null || database.Songs.Length == 0)
        {
            EditorUtility.DisplayDialog("错误", "数据库中没有歌曲", "确定");
            return;
        }

        // 创建歌曲选择列表
        string[] songNames = new string[database.Songs.Length];
        for (int i = 0; i < database.Songs.Length; i++)
        {
            string clipName = database.Songs[i].AudioClip != null ? database.Songs[i].AudioClip.name : "No Clip";
            songNames[i] = $"{i}: {database.Songs[i].SongName} ({clipName})";
        }

        // 显示选择窗口
        int selectedIndex = EditorGUILayout.Popup("选择歌曲", 0, songNames);

        // 应用谱面
        string json = GenerateBeatmapJson();
        database.Songs[selectedIndex].BeatmapJson = json;

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        // 修复：使用正确的变量名 bpm
        Debug.Log($"已将{bpm} BPM谱面应用到: {database.Songs[selectedIndex].SongName}");
        ShowNotification(new GUIContent("谱面已应用到数据库!"));
    }

    // 辅助方法：统计JSON中的音符数量
    private int CountNotesInJson(string json)
    {
        // 简单的方法：计算 "{" 的数量减去2（因为JSON结构中有其他大括号）
        int count = 0;
        foreach (char c in json)
        {
            if (c == '{') count++;
        }
        return count - 2; // 减去外层的两个大括号
    }

    // 辅助方法：计算总时长
    private float CalculateTotalTime(string json)
    {
        // 从JSON中提取最大的T值
        int lastNoteIndex = json.LastIndexOf("\"T\":");
        if (lastNoteIndex == -1) return 0f;

        // 提取时间值
        int timeStart = lastNoteIndex + 4;
        int timeEnd = json.IndexOf(',', timeStart);
        if (timeEnd == -1) timeEnd = json.IndexOf('}', timeStart);

        if (timeEnd > timeStart)
        {
            string timeStr = json.Substring(timeStart, timeEnd - timeStart).Trim();
            if (int.TryParse(timeStr, out int timeMs))
            {
                return timeMs / 1000f;
            }
        }

        return 0f;
    }
}