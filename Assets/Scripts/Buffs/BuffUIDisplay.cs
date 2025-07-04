using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Buffs.UI
{
    /// <summary>
    /// 显示Buff/Debuff图标和持续时间
    /// </summary>
    public class BuffUIDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform buffContainer;
        [SerializeField] private GameObject buffIconPrefab;
        [SerializeField] private float iconSize = 32f;
        [SerializeField] private float iconSpacing = 4f;
        
        [Header("Display Settings")]
        [SerializeField] private bool separateBuffsAndDebuffs = true;
        [SerializeField] private int maxIconsPerRow = 10;
        
        private BuffManager buffManager;
        private Dictionary<string, BuffIconUI> activeIcons = new Dictionary<string, BuffIconUI>();
        
        private void Start()
        {
            // 查找BuffManager
            buffManager = GetComponentInParent<BuffManager>();
            if (buffManager == null)
            {
                Debug.LogError("[BuffUIDisplay] No BuffManager found!");
                enabled = false;
                return;
            }
            
            // 订阅事件
            buffManager.OnBuffAdded += OnBuffAdded;
            buffManager.OnBuffRemoved += OnBuffRemoved;
            buffManager.OnBuffStackChanged += OnBuffStackChanged;
        }
        
        private void OnDestroy()
        {
            if (buffManager != null)
            {
                buffManager.OnBuffAdded -= OnBuffAdded;
                buffManager.OnBuffRemoved -= OnBuffRemoved;
                buffManager.OnBuffStackChanged -= OnBuffStackChanged;
            }
        }
        
        private void Update()
        {
            // 更新所有图标的持续时间显示
            foreach (var kvp in activeIcons)
            {
                var buff = buffManager.GetBuff(kvp.Key);
                if (buff != null)
                {
                    kvp.Value.UpdateDisplay(buff);
                }
            }
        }
        
        private void OnBuffAdded(BuffInstance buff)
        {
            if (activeIcons.ContainsKey(buff.BuffId)) return;
            
            // 创建新图标
            var iconGO = CreateBuffIcon();
            var iconUI = iconGO.GetComponent<BuffIconUI>();
            
            if (iconUI != null)
            {
                iconUI.SetupBuff(buff);
                activeIcons[buff.BuffId] = iconUI;
            }
            
            RefreshLayout();
        }
        
        private void OnBuffRemoved(BuffInstance buff)
        {
            if (activeIcons.TryGetValue(buff.BuffId, out var icon))
            {
                Destroy(icon.gameObject);
                activeIcons.Remove(buff.BuffId);
                RefreshLayout();
            }
        }
        
        private void OnBuffStackChanged(BuffInstance buff)
        {
            if (activeIcons.TryGetValue(buff.BuffId, out var icon))
            {
                icon.UpdateDisplay(buff);
            }
        }
        
        private GameObject CreateBuffIcon()
        {
            if (buffIconPrefab != null)
            {
                return Instantiate(buffIconPrefab, buffContainer);
            }
            
            // 如果没有预制体，创建一个简单的
            var iconGO = new GameObject("BuffIcon");
            iconGO.transform.SetParent(buffContainer);
            
            var rect = iconGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(iconSize, iconSize);
            
            var image = iconGO.AddComponent<Image>();
            image.color = Color.white;
            
            iconGO.AddComponent<BuffIconUI>();
            
            return iconGO;
        }
        
        private void RefreshLayout()
        {
            if (!separateBuffsAndDebuffs)
            {
                // 简单排列
                int index = 0;
                foreach (var icon in activeIcons.Values)
                {
                    PositionIcon(icon.gameObject, index);
                    index++;
                }
            }
            else
            {
                // 分开排列Buff和Debuff
                int buffIndex = 0;
                int debuffIndex = 0;
                
                foreach (var kvp in activeIcons)
                {
                    var buff = buffManager.GetBuff(kvp.Key);
                    if (buff != null)
                    {
                        if (buff.Type == BuffType.Buff)
                        {
                            PositionIcon(kvp.Value.gameObject, buffIndex, 0);
                            buffIndex++;
                        }
                        else
                        {
                            PositionIcon(kvp.Value.gameObject, debuffIndex, 1);
                            debuffIndex++;
                        }
                    }
                }
            }
        }
        
        private void PositionIcon(GameObject icon, int index, int row = 0)
        {
            var rect = icon.GetComponent<RectTransform>();
            if (rect == null) return;
            
            int col = index % maxIconsPerRow;
            float x = col * (iconSize + iconSpacing);
            float y = -row * (iconSize + iconSpacing * 2);
            
            rect.anchoredPosition = new Vector2(x, y);
        }
    }
    
    /// <summary>
    /// 单个Buff图标的UI组件
    /// </summary>
    public class BuffIconUI : MonoBehaviour
    {
        private Image iconImage;
        private Text durationText;
        private Text stackText;
        private Image cooldownFill;
        
        private void Awake()
        {
            iconImage = GetComponent<Image>();
            
            // 创建持续时间文本
            var durationGO = new GameObject("Duration");
            durationGO.transform.SetParent(transform);
            durationText = durationGO.AddComponent<Text>();
            durationText.alignment = TextAnchor.LowerRight;
            durationText.fontSize = 12;
            durationText.color = Color.white;
            
            var durationRect = durationGO.GetComponent<RectTransform>();
            durationRect.anchorMin = Vector2.zero;
            durationRect.anchorMax = Vector2.one;
            durationRect.offsetMin = new Vector2(2, 2);
            durationRect.offsetMax = new Vector2(-2, -2);
            
            // 创建层数文本
            var stackGO = new GameObject("Stack");
            stackGO.transform.SetParent(transform);
            stackText = stackGO.AddComponent<Text>();
            stackText.alignment = TextAnchor.UpperLeft;
            stackText.fontSize = 14;
            stackText.fontStyle = FontStyle.Bold;
            stackText.color = Color.yellow;
            
            var stackRect = stackGO.GetComponent<RectTransform>();
            stackRect.anchorMin = Vector2.zero;
            stackRect.anchorMax = Vector2.one;
            stackRect.offsetMin = new Vector2(2, 2);
            stackRect.offsetMax = new Vector2(-2, -2);
        }
        
        public void SetupBuff(BuffInstance buff)
        {
            if (buff.Data.Icon != null)
            {
                iconImage.sprite = buff.Data.Icon;
            }
            
            // 设置边框颜色表示类型
            if (buff.Type == BuffType.Debuff)
            {
                iconImage.color = new Color(1f, 0.5f, 0.5f);
            }
            else if (buff.Type == BuffType.Buff)
            {
                iconImage.color = new Color(0.5f, 1f, 0.5f);
            }
            
            UpdateDisplay(buff);
        }
        
        public void UpdateDisplay(BuffInstance buff)
        {
            // 更新持续时间
            if (!buff.Data.IsPermanent && durationText != null)
            {
                if (buff.RemainingTime < 10f)
                {
                    durationText.text = buff.RemainingTime.ToString("F1");
                    durationText.color = buff.RemainingTime < 3f ? Color.red : Color.white;
                }
                else
                {
                    durationText.text = Mathf.CeilToInt(buff.RemainingTime).ToString();
                }
            }
            else if (durationText != null)
            {
                durationText.text = "";
            }
            
            // 更新层数
            if (stackText != null)
            {
                if (buff.CurrentStacks > 1)
                {
                    stackText.text = buff.CurrentStacks.ToString();
                }
                else
                {
                    stackText.text = "";
                }
            }
            
            // 更新冷却填充
            if (cooldownFill != null && !buff.Data.IsPermanent)
            {
                cooldownFill.fillAmount = buff.GetRemainingPercentage();
            }
        }
    }
}