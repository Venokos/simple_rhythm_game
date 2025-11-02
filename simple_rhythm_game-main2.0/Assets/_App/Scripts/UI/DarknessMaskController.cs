using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DarknessMaskController : MonoBehaviour
{
    [Header("遮罩设置")]
    [SerializeField] private CanvasGroup maskCanvasGroup;
    [SerializeField] private Image maskImage;

    [Header("时间设置")]
    [SerializeField] private float effectStartTime = 8f;
    [SerializeField] private float effectEndTime = 18f;
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("击中恢复设置")]
    [SerializeField] private int maxComboToClear = 7;
    [SerializeField] private float baseRadius = 0.15f;

    [Header("调试")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private bool forceTestMode = false; // 强制测试模式

    // 材质属性名称
    private const string CENTER_X_PROPERTY = "_CenterX";
    private const string CENTER_Y_PROPERTY = "_CenterY";
    private const string RADIUS_PROPERTY = "_Radius";
    private const string PROGRESS_PROPERTY = "_Progress";

    private SongController songController;
    private Material dynamicMaterial;
    private float currentProgress = 0f;
    private int currentCombo = 0;
    private bool isEffectActive = false;
    private bool isFullyCleared = false;
    private Coroutine effectCoroutine;

    // 属性访问器，用于调试
    public float CurrentProgress => currentProgress;
    public int CurrentCombo => currentCombo;
    public bool IsEffectActive => isEffectActive;

    private void Start()
    {
        songController = FindObjectOfType<SongController>();

        if (maskImage == null)
        {
            Debug.LogError("DarknessMaskController: Mask Image 未分配!");
            return;
        }

        // 创建动态材质实例
        dynamicMaterial = new Material(maskImage.material);
        maskImage.material = dynamicMaterial;
        maskImage.raycastTarget = false;

        // 初始化材质属性
        InitializeMaterialProperties();

        if (enableDebug) Debug.Log("DarknessMaskController: 初始化完成");

        // 如果启用强制测试模式，添加测试按键
        if (forceTestMode && enableDebug)
        {
            Debug.Log("DarknessMaskController: 强制测试模式已启用 - 按T键增加进度，按R键重置");
        }
    }

    private void Update()
    {
        // 测试模式按键
        if (forceTestMode && enableDebug)
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                TestProgress(currentProgress + 0.14f);
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetMask();
            }
        }

        if (songController == null || isFullyCleared) return;

        double songTimeSeconds = songController.SongTimeMsDSP / 1000.0;

        // 检查是否在效果时间段内
        bool shouldBeActive = songTimeSeconds >= effectStartTime && songTimeSeconds <= effectEndTime;

        if (shouldBeActive && !isEffectActive)
        {
            StartEffect();
        }
        else if (!shouldBeActive && isEffectActive && !isFullyCleared)
        {
            EndEffect();
        }
    }

    private void InitializeMaterialProperties()
    {
        if (dynamicMaterial != null)
        {
            // 检查材质属性是否存在
            bool hasCenterX = dynamicMaterial.HasProperty(CENTER_X_PROPERTY);
            bool hasCenterY = dynamicMaterial.HasProperty(CENTER_Y_PROPERTY);
            bool hasRadius = dynamicMaterial.HasProperty(RADIUS_PROPERTY);
            bool hasProgress = dynamicMaterial.HasProperty(PROGRESS_PROPERTY);

            if (enableDebug)
            {
                Debug.Log($"DarknessMaskController: 材质属性检查 - CenterX:{hasCenterX}, CenterY:{hasCenterY}, Radius:{hasRadius}, Progress:{hasProgress}");
            }

            // 设置初始属性值
            if (hasCenterX) dynamicMaterial.SetFloat(CENTER_X_PROPERTY, 0.5f);
            if (hasCenterY) dynamicMaterial.SetFloat(CENTER_Y_PROPERTY, 0.3f);
            if (hasRadius) dynamicMaterial.SetFloat(RADIUS_PROPERTY, baseRadius);
            if (hasProgress) dynamicMaterial.SetFloat(PROGRESS_PROPERTY, 0f);

            if (enableDebug) Debug.Log($"DarknessMaskController: 材质属性初始化 - 半径={baseRadius}, 进度=0");
        }
        else
        {
            Debug.LogError("DarknessMaskController: 动态材质为空!");
        }
    }

    private void StartEffect()
    {
        isEffectActive = true;
        currentCombo = 0;
        currentProgress = 0f;
        isFullyCleared = false;

        // 显示遮罩
        if (maskCanvasGroup != null)
        {
            maskCanvasGroup.alpha = 1f;
        }

        // 重置材质属性
        if (dynamicMaterial != null)
        {
            dynamicMaterial.SetFloat(PROGRESS_PROPERTY, 0f);
            UpdateMaterialProperties();
        }

        if (enableDebug) Debug.Log("DarknessMaskController: 黑暗效果开始");

        // 开始效果协程
        if (effectCoroutine != null) StopCoroutine(effectCoroutine);
        effectCoroutine = StartCoroutine(EffectRoutine());
    }

    private void EndEffect()
    {
        isEffectActive = false;

        // 如果还没有完全恢复，隐藏遮罩
        if (!isFullyCleared && maskCanvasGroup != null)
        {
            maskCanvasGroup.alpha = 0f;
        }

        if (enableDebug) Debug.Log("DarknessMaskController: 黑暗效果结束");
    }

    private IEnumerator EffectRoutine()
    {
        while (isEffectActive && !isFullyCleared)
        {
            UpdateEffect();
            yield return null;
        }
    }

    private void UpdateEffect()
    {
        if (songController == null) return;

        double songTimeSeconds = songController.SongTimeMsDSP / 1000.0;

        // 计算淡入淡出
        float fadeFactor = 1f;

        if (songTimeSeconds < effectStartTime + fadeInDuration)
        {
            // 淡入阶段
            fadeFactor = (float)(songTimeSeconds - effectStartTime) / fadeInDuration;
        }
        else if (songTimeSeconds > effectEndTime - fadeOutDuration && !isFullyCleared)
        {
            // 淡出阶段（除非已经完全恢复）
            fadeFactor = (float)(effectEndTime - songTimeSeconds) / fadeOutDuration;
        }

        // 应用淡入淡出
        if (maskCanvasGroup != null && !isFullyCleared)
        {
            maskCanvasGroup.alpha = fadeFactor;
        }
    }

    private void UpdateMaterialProperties()
    {
        if (dynamicMaterial != null)
        {
            // 检查材质属性是否存在
            bool hasCenterX = dynamicMaterial.HasProperty(CENTER_X_PROPERTY);
            bool hasCenterY = dynamicMaterial.HasProperty(CENTER_Y_PROPERTY);
            bool hasRadius = dynamicMaterial.HasProperty(RADIUS_PROPERTY);
            bool hasProgress = dynamicMaterial.HasProperty(PROGRESS_PROPERTY);

            // 设置中心点（判定线位置）
            if (hasCenterX) dynamicMaterial.SetFloat(CENTER_X_PROPERTY, 0.5f);
            if (hasCenterY) dynamicMaterial.SetFloat(CENTER_Y_PROPERTY, 0.3f);

            // 设置基础半径
            if (hasRadius) dynamicMaterial.SetFloat(RADIUS_PROPERTY, baseRadius);

            // 设置恢复进度
            if (hasProgress) dynamicMaterial.SetFloat(PROGRESS_PROPERTY, currentProgress);

            // 强制更新材质
            if (maskImage != null)
            {
                maskImage.SetMaterialDirty();
            }

            if (enableDebug)
            {
                float actualRadius = Mathf.Lerp(baseRadius, 1.0f, currentProgress);
                Debug.Log($"DarknessMaskController: 材质更新 - 进度={currentProgress:F2}, 实际半径={actualRadius:F3}, 属性存在:[{hasCenterX},{hasCenterY},{hasRadius},{hasProgress}]");
            }
        }
        else
        {
            Debug.LogError("DarknessMaskController: 更新材质属性时动态材质为空!");
        }
    }

    // 当音符被击中时调用
    public void OnNoteHit(JudgeType judgeType)
    {
        if (!isEffectActive || judgeType == JudgeType.Miss || isFullyCleared)
        {
            if (enableDebug) Debug.Log($"DarknessMaskController: 击中忽略 - 效果激活={isEffectActive}, 判定类型={judgeType}, 已完全恢复={isFullyCleared}");
            return;
        }

        // 增加连击
        currentCombo++;

        // 计算恢复进度
        currentProgress = Mathf.Min(1.0f, (float)currentCombo / maxComboToClear);

        if (enableDebug) Debug.Log($"DarknessMaskController: 击中音符 - 连击: {currentCombo}, 恢复进度: {currentProgress:F2}");

        // 更新材质属性
        UpdateMaterialProperties();

        // 检查是否完全恢复
        if (currentCombo >= maxComboToClear)
        {
            FullyClearMask();
        }
    }

    // 当音符Miss时调用
    public void OnNoteMiss()
    {
        if (!isEffectActive || isFullyCleared)
        {
            if (enableDebug) Debug.Log($"DarknessMaskController: Miss忽略 - 效果激活={isEffectActive}, 已完全恢复={isFullyCleared}");
            return;
        }

        // 重置连击和进度
        currentCombo = 0;
        currentProgress = 0f;

        if (enableDebug) Debug.Log($"DarknessMaskController: Miss! 连击重置");

        // 更新材质属性
        UpdateMaterialProperties();
    }

    // 完全清除遮罩
    private void FullyClearMask()
    {
        isFullyCleared = true;

        // 隐藏遮罩
        if (maskCanvasGroup != null)
        {
            maskCanvasGroup.alpha = 0f;
        }

        if (enableDebug) Debug.Log("DarknessMaskController: 达成7连击! 屏幕完全恢复");
    }

    // 提供给外部调用的方法
    public void RegisterHit(JudgeType judgeType)
    {
        if (enableDebug) Debug.Log($"DarknessMaskController: 注册击中 - {judgeType}");

        if (judgeType == JudgeType.Miss)
        {
            OnNoteMiss();
        }
        else
        {
            OnNoteHit(judgeType);
        }
    }

    // 重置遮罩状态
    public void ResetMask()
    {
        isEffectActive = false;
        isFullyCleared = false;
        currentCombo = 0;
        currentProgress = 0f;

        if (effectCoroutine != null)
        {
            StopCoroutine(effectCoroutine);
            effectCoroutine = null;
        }

        if (maskCanvasGroup != null)
        {
            maskCanvasGroup.alpha = 0f;
        }

        if (dynamicMaterial != null)
        {
            dynamicMaterial.SetFloat(PROGRESS_PROPERTY, 0f);
            UpdateMaterialProperties();
        }

        if (enableDebug) Debug.Log("DarknessMaskController: 遮罩状态重置");
    }

    // 手动测试方法 - 用于调试
    public void TestProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);
        UpdateMaterialProperties();
        if (enableDebug) Debug.Log($"DarknessMaskController: 测试进度设置为 {progress}");
    }
}