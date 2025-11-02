using System;
using DG.Tweening;
using UnityEngine;

public class RhythmNote : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _visual;

    [Header("Hit Effect Settings")]
    [SerializeField] private ParticleSystem _hitEffectPrefab;  // 特效预制体

    private double _targetTimeMs;
    private float _laneHeight = 6f;
    private float _leadMs = 1500f;
    private double _tUntil;

    public bool IsOutOfScreen => _tUntil < -100.0;
    public double TargetTimeMs => _targetTimeMs;

    public void Init(double targetTimeMs, float leadMs, float laneHeight)
    {
        _targetTimeMs = targetTimeMs;
        _leadMs = leadMs;
        _laneHeight = laneHeight;
        _tUntil = leadMs;
        _visual.DOFade(1f, 0);

        var p = 1f - Mathf.Clamp01((float)((_leadMs - _tUntil) / _leadMs));
        var y = Mathf.Lerp(0f, _laneHeight, p);
        transform.localPosition = new Vector3(0f, y, 0f);
    }

    public void Move(double songTimeMsDSP)
    {
        _tUntil = _targetTimeMs - songTimeMsDSP;
        var p = 1f - Mathf.Clamp01((float)((_leadMs - _tUntil) / _leadMs));
        var y = Mathf.Lerp(0f, _laneHeight, p);
        transform.localPosition = new Vector3(0f, y, 0f);
    }

    // 当音符被点击时调用
    public void OnHit(JudgeType judgeType, Action onComplete = null)
    {
        PlayHitEffect(judgeType);
        PlayDisappearAnimation(onComplete);
    }

    // 当音符Miss时调用
    public void OnMiss(Action onComplete = null)
    {
        PlayHitEffect(JudgeType.Miss);
        PlayDisappearAnimation(onComplete);
    }

    // 播放点击特效
    private void PlayHitEffect(JudgeType judgeType)
    {
        if (_hitEffectPrefab != null)
        {
            ParticleSystem effectInstance = Instantiate(_hitEffectPrefab, transform.position, Quaternion.identity);

            // 配置特效
            var main = effectInstance.main;
            switch (judgeType)
            {
                case JudgeType.Perfect:
                    main.startSpeedMultiplier = 3f;
                    main.startSizeMultiplier = 1.5f;
                    break;
                case JudgeType.Good:
                    main.startSpeedMultiplier = 2f;
                    main.startSizeMultiplier = 1.2f;
                    break;
                case JudgeType.Miss:
                    main.startSpeedMultiplier = 1f;
                    main.startSizeMultiplier = 0.8f;
                    break;
            }

            effectInstance.Play();
            Destroy(effectInstance.gameObject, main.duration);
        }
    }

    public void PlayDisappearAnimation(Action onComplete = null)
    {
        _visual.DOFade(0, 0.3f).OnComplete(() => { onComplete?.Invoke(); });
    }
}