using System;
using System.Collections.Generic;
using UnityEngine;

public class GameplayController : MonoBehaviour
{
    [Header("CONFIGS")]
    [SerializeField] private SongDatabaseSO _songDatabase;
    [SerializeField] private float _laneHeight = 8f;
    [SerializeField] private float _spawnLeadTimeMs = 1500f;
    [SerializeField] private int _scorePerPerfect = 100;
    [SerializeField] private int _scorePerGood = 50;
    [SerializeField] private float _perfectMs = 60f;
    [SerializeField] private float _goodMs = 110f;

    [Header("CONTROLLERS")]
    [SerializeField] private NoteFactory _noteFactory;
    [SerializeField] private SongController _songController;
    [SerializeField] private DarknessMaskController _darknessMaskController;

    [Header("LANES")]
    [SerializeField] private LayerMask _noteMask;

    [Header("调试")]
    [SerializeField] private bool enableDebug = true;

    private GameScoreData _currentScoreData;
    private GameplayScreen _gameplayScreen;

    private bool _isPlaying = false;
    private int _currentNoteIndex;
    private MiniChart _chart;

    private readonly List<RhythmNote> _currentNotes = new();

    private void Start()
    {
        EventBus.Subscribe<PlayButtonClickedEvent>(OnPlayButtonClicked);

        // 自动查找必要的组件
        AutoFindComponents();
    }

    private void AutoFindComponents()
    {
        // 自动查找 DarknessMaskController
        if (_darknessMaskController == null)
        {
            _darknessMaskController = FindObjectOfType<DarknessMaskController>();
            if (_darknessMaskController != null)
            {
                if (enableDebug) Debug.Log("GameplayController: 自动找到 DarknessMaskController");
            }
            else
            {
                Debug.LogError("GameplayController: 无法找到 DarknessMaskController，请确保场景中有该组件");
            }
        }

        // 自动查找其他必要组件（可选）
        if (_songController == null)
        {
            _songController = FindObjectOfType<SongController>();
            if (_songController != null && enableDebug)
                Debug.Log("GameplayController: 自动找到 SongController");
        }

        if (_noteFactory == null)
        {
            _noteFactory = FindObjectOfType<NoteFactory>();
            if (_noteFactory != null && enableDebug)
                Debug.Log("GameplayController: 自动找到 NoteFactory");
        }
    }

    public void StartGameplay()
    {
        InitData();
        InitUI();
        HandleSpawnNotes();
        _songController.StartSong();
        return;

        void InitData()
        {
            // 只从启用的歌曲中选择
            var enabledSongs = new List<SongInfo>();
            foreach (var song in _songDatabase.Songs)
            {
                if (song.IsEnabled)
                    enabledSongs.Add(song);
            }

            if (enabledSongs.Count == 0)
            {
                Debug.LogError("没有启用的歌曲！");
                return;
            }

            var randomSongIndex = UnityEngine.Random.Range(0, enabledSongs.Count);
            var songInfo = enabledSongs[randomSongIndex];
            _currentScoreData = new();
            _isPlaying = true;
            _chart = songInfo.GetChart();
            _currentNotes.Clear();
            _currentNoteIndex = 0;
            _songController.LoadSong(songInfo.AudioClip, _chart.OffsetMs);

            // 重置遮罩状态
            if (_darknessMaskController != null)
            {
                _darknessMaskController.ResetMask();
                if (enableDebug) Debug.Log("GameplayController: 遮罩状态已重置");
            }
            else
            {
                Debug.LogError("GameplayController: DarknessMaskController 为空，无法重置遮罩");
            }

            if (enableDebug) Debug.Log($"GameplayController: 选择歌曲 - {songInfo.SongName}, 启用的歌曲数量: {enabledSongs.Count}");
        }

        void InitUI()
        {
            _gameplayScreen = UIManager.Instance.ShowScreen<GameplayScreen>();
            _gameplayScreen.UpdateScore(0);
            _gameplayScreen.UpdateProgress(1);
            _gameplayScreen.UpdateCombo(0);
        }
    }

    private void HandleSpawnNotes()
    {
        double currentSongMs = _songController.SongTimeMsDSP;
        while (_currentNoteIndex < _chart.Notes.Length)
        {
            var noteData = _chart.Notes[_currentNoteIndex];
            if (noteData.T - currentSongMs <= _spawnLeadTimeMs)
            {
                var note = _noteFactory.SpawnNote(noteData.Lane);
                note.Init(targetTimeMs: noteData.T, leadMs: _spawnLeadTimeMs, laneHeight: _laneHeight);
                _currentNotes.Add(note);
                _currentNoteIndex++;
            }
            else break;
        }
    }

    private void Update()
    {
        if (!_isPlaying) return;
        UpdateSongProgressBar();
        HandleSpawnNotes();
        HandleMoveNotes();
        HandleJudgeTapNote();
        HandleDespawnNotes();
        CheckForCompletion();
    }

    private void UpdateSongProgressBar()
    {
        var progress = 1 - _songController.SongTimeMsDSP / _songController.SongLengthMs;
        _gameplayScreen.UpdateProgress((float)progress);
    }

    private void HandleMoveNotes()
    {
        foreach (var note in _currentNotes)
        {
            note.Move(_songController.SongTimeMsDSP);
        }
    }

    private void HandleJudgeTapNote()
    {
        var songMs = _songController.SongTimeMsDSP;
        var note = GetTapNote();
        if (note == null) return; // No note tap
        var judgeType = GetJudgement(note.TargetTimeMs, songMs);
        var score = judgeType switch
        {
            JudgeType.Perfect => _scorePerPerfect,
            JudgeType.Good => _scorePerGood,
            _ => 0
        };
        if (score > 0) // Hit
        {
            _currentScoreData.Score += score;
            _gameplayScreen.UpdateScore(_currentScoreData.Score);
            _currentScoreData.CurrentCombo++;
            if (_currentScoreData.CurrentCombo > _currentScoreData.MaxCombo)
            {
                _currentScoreData.MaxCombo = _currentScoreData.CurrentCombo;
            }
        }
        else // Miss
        {
            _currentScoreData.Misses++;
            _currentScoreData.CurrentCombo = 0;
        }
        _gameplayScreen.ShowJudgeText(judgeType);
        _gameplayScreen.UpdateCombo(_currentScoreData.CurrentCombo);
        _currentNotes.Remove(note);

        // 通知遮罩控制器
        if (_darknessMaskController != null)
        {
            if (enableDebug) Debug.Log($"GameplayController: 通知遮罩控制器 - 判定类型: {judgeType}, 当前连击: {_currentScoreData.CurrentCombo}");
            _darknessMaskController.RegisterHit(judgeType);
        }
        else
        {
            Debug.LogError("GameplayController: DarknessMaskController 引用为空! 无法通知击中事件");
        }

        if (judgeType == JudgeType.Miss)
        {
            note.OnMiss(() => _noteFactory.ReleaseNote(note));
        }
        else
        {
            note.OnHit(judgeType, () => _noteFactory.ReleaseNote(note));
        }
    }

    /// <summary>
    /// Despawn note if it goes out of screen and count as miss
    /// </summary>
    private RhythmNote GetTapNote()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics2D.Raycast(ray.origin, ray.direction, 100f, _noteMask))
            {
                var hit2D = Physics2D.Raycast(ray.origin, ray.direction, 100f, _noteMask);
                var note = hit2D.collider?.GetComponent<RhythmNote>();
                return note;
            }
        }
        return null;
    }

    /// <summary>
    /// Despawn note if it goes out of screen and count as miss
    /// </summary>
    private void HandleDespawnNotes()
    {
        foreach (var note in _currentNotes.ToArray())
        {
            if (note.IsOutOfScreen)
            {
                _currentNotes.Remove(note);

                // 通知遮罩控制器
                if (_darknessMaskController != null)
                {
                    if (enableDebug) Debug.Log($"GameplayController: 音符Miss - 通知遮罩控制器");
                    _darknessMaskController.RegisterHit(JudgeType.Miss);
                }
                else
                {
                    Debug.LogError("GameplayController: DarknessMaskController 引用为空! 无法通知Miss事件");
                }

                note.OnMiss(() => _noteFactory.ReleaseNote(note));
                _currentScoreData.Misses++;
                _currentScoreData.CurrentCombo = 0;
                _gameplayScreen.UpdateCombo(_currentScoreData.CurrentCombo);
                _gameplayScreen.ShowJudgeText(JudgeType.Miss);
            }
        }
    }

    private void CheckForCompletion()
    {
        if (_songController.IsCompleted && _currentNotes.Count == 0)
        {
            _isPlaying = false;
            UIManager.Instance.HideScreen<GameplayScreen>();
            UIManager.Instance.ShowScreen<WinScreen>()
                .SetData(_currentScoreData.Score, _currentScoreData.MaxCombo);
        }
    }

    private void OnPlayButtonClicked(PlayButtonClickedEvent eventData)
    {
        StartGameplay();
    }

    private JudgeType GetJudgement(double noteTargetTimeMs, double songTimeMsDSP)
    {
        double d = System.Math.Abs(noteTargetTimeMs - songTimeMsDSP);
        if (d <= _perfectMs)
        {
            return JudgeType.Perfect;
        }
        else if (d <= _goodMs)
        {
            return JudgeType.Good;
        }
        return JudgeType.Miss;
    }
}

[Serializable]
public class GameScoreData
{
    public int Score;
    public int MaxCombo;
    public int CurrentCombo;
    public int Misses;
}