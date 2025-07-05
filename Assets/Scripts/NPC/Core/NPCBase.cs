using UnityEngine;
using NPC.Interfaces;
using NPC.Data;

namespace NPC.Core
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class NPCBase : MonoBehaviour, INPCInteractable
    {
        [Header("NPC Configuration")]
        [SerializeField] protected NPCData npcData;
        
        [Header("Interaction")]
        [SerializeField] protected GameObject currentInteractor;
        [SerializeField] protected bool isInteracting = false;
        [SerializeField] protected float lastInteractionTime;
        
        [Header("Visual")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected GameObject interactionPrompt;
        [SerializeField] protected float promptOffset = 1.5f;
        
        // Properties
        public NPCType NPCType => npcData?.npcType ?? NPCType.Merchant;
        public NPCInteractionType InteractionType => npcData?.interactionType ?? NPCInteractionType.Shop;
        public NPCData Data => npcData;
        public bool IsInteracting => isInteracting;
        
        /// <summary>
        /// 设置NPC数据（用于运行时设置）
        /// </summary>
        /// <param name="data">要设置的NPC数据</param>
        /// <param name="forceOverwrite">是否强制覆盖已有数据</param>
        public void SetNPCData(NPCData data, bool forceOverwrite = false)
        {
            if (data == null)
            {
                Debug.LogError($"[NPCBase] Attempting to set null NPCData on {name}");
                return;
            }
            
            // 如果预制体已经有npcData，并且和要设置的相同，就不需要覆盖
            if (npcData != null && npcData == data)
            {
                Debug.Log($"[NPCBase] {name} already has the correct NPC data: {data.npcName}");
                return;
            }
            
            // 如果预制体有不同的npcData，并且不是强制覆盖，优先使用预制体的设置
            if (npcData != null && !forceOverwrite)
            {
                Debug.LogWarning($"[NPCBase] {name} already has NPC data ({npcData.npcName}), keeping existing data instead of setting {data.npcName}. Use forceOverwrite=true to override.");
                return;
            }
            
            // 如果是强制覆盖，记录日志
            if (npcData != null && forceOverwrite)
            {
                Debug.Log($"[NPCBase] Force overwriting NPC data on {name} from {npcData.npcName} to {data.npcName}");
            }
            
            npcData = data;
            Debug.Log($"[NPCBase] Set NPC data on {name} to {data.npcName} ({data.npcType}), Data file: {data.name}");
            
            // 确保组件已设置
            if (spriteRenderer == null)
            {
                SetupComponents();
            }
            
            // 更新视觉效果
            SetupVisuals();
        }
        
        protected virtual void Awake()
        {
            SetupComponents();
            // 不在Awake中调用SetupVisuals，因为此时npcData可能还没有设置
            // SetupVisuals会在SetNPCData中被调用
        }
        
        protected virtual void Start()
        {
            CreateInteractionPrompt();
        }
        
        protected virtual void Update()
        {
            UpdateInteractionPrompt();
            
            // 检查交互者是否还在范围内
            if (isInteracting && currentInteractor != null)
            {
                float distance = Vector2.Distance(transform.position, currentInteractor.transform.position);
                if (distance > npcData.interactionRange * 1.5f)
                {
                    EndInteraction();
                }
            }
        }
        
        protected virtual void SetupComponents()
        {
            // 确保有碰撞器
            var collider = GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            // 获取或创建SpriteRenderer
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                GameObject visual = new GameObject("Visual");
                visual.transform.SetParent(transform);
                visual.transform.localPosition = Vector3.zero;
                spriteRenderer = visual.AddComponent<SpriteRenderer>();
            }
        }
        
        protected virtual void SetupVisuals()
        {
            if (spriteRenderer != null)
            {
                // 确保SpriteRenderer在正确的排序层
                spriteRenderer.sortingLayerName = "Default";
                spriteRenderer.sortingOrder = 10;
                
                // 只有当npcData有sprite时才覆盖，否则保持预制体中的设置
                if (npcData != null)
                {
                    if (npcData.npcSprite != null)
                    {
                        spriteRenderer.sprite = npcData.npcSprite;
                    }
                    spriteRenderer.color = npcData.npcColor;
                }
            }
        }
        
        protected virtual void CreateInteractionPrompt()
        {
            if (interactionPrompt == null)
            {
                interactionPrompt = new GameObject("InteractionPrompt");
                interactionPrompt.transform.SetParent(transform);
                interactionPrompt.transform.localPosition = Vector3.up * promptOffset;
                
                // 添加文本或图标提示
                var textMesh = interactionPrompt.AddComponent<TextMesh>();
                textMesh.text = "[E]";
                textMesh.characterSize = 0.1f;
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.color = Color.white;
            }
            
            interactionPrompt.SetActive(false);
        }
        
        protected virtual void UpdateInteractionPrompt()
        {
            if (interactionPrompt == null) return;
            
            // 始终面向相机
            if (Camera.main != null)
            {
                interactionPrompt.transform.rotation = Camera.main.transform.rotation;
            }
        }
        
        // INPCInteractable 实现
        public virtual bool CanInteract(GameObject interactor)
        {
            if (isInteracting && currentInteractor != interactor) return false;
            if (npcData == null) return false;
            
            float distance = Vector2.Distance(transform.position, interactor.transform.position);
            return distance <= npcData.interactionRange;
        }
        
        public virtual void StartInteraction(GameObject interactor)
        {
            Debug.Log($"[NPCBase] StartInteraction called on {name} by {interactor.name}");
            
            if (!CanInteract(interactor))
            {
                Debug.Log($"[NPCBase] Cannot interact: CanInteract returned false");
                return;
            }
            
            if (npcData == null)
            {
                Debug.LogError($"[NPCBase] npcData is null on {name}!");
                return;
            }
            
            isInteracting = true;
            currentInteractor = interactor;
            lastInteractionTime = Time.time;
            
            // 播放问候语音
            if (npcData.greetingSound != null)
            {
                AudioSource.PlayClipAtPoint(npcData.greetingSound, transform.position);
            }
            
            // 显示对话
            Debug.Log($"[NPCBase] Showing greeting: {npcData.greetingText}");
            ShowDialogue(npcData.greetingText);
            
            // 子类实现具体交互逻辑
            OnInteractionStarted(interactor);
        }
        
        public virtual void EndInteraction()
        {
            if (!isInteracting) return;
            
            // 播放告别语音
            if (npcData.farewellSound != null)
            {
                AudioSource.PlayClipAtPoint(npcData.farewellSound, transform.position);
            }
            
            // 显示告别对话
            ShowDialogue(npcData.farewellText);
            
            // 子类清理
            OnInteractionEnded();
            
            isInteracting = false;
            currentInteractor = null;
        }
        
        public virtual string GetInteractionPrompt()
        {
            return $"[E] 与{npcData.npcName}交谈";
        }
        
        // 子类需要重写的方法
        protected abstract void OnInteractionStarted(GameObject interactor);
        protected abstract void OnInteractionEnded();
        
        // 辅助方法
        protected virtual void ShowDialogue(string text)
        {
            // TODO: 实现对话UI显示
            if (npcData != null)
            {
                Debug.Log($"{npcData.npcName}: {text}");
            }
            else
            {
                Debug.Log($"[Unknown NPC]: {text}");
            }
        }
        
        // 触发器事件
        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") && !isInteracting)
            {
                interactionPrompt?.SetActive(true);
            }
        }
        
        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                interactionPrompt?.SetActive(false);
                
                if (isInteracting && other.gameObject == currentInteractor)
                {
                    EndInteraction();
                }
            }
        }
        
        // 编辑器辅助
        protected virtual void OnDrawGizmosSelected()
        {
            if (npcData != null)
            {
                // 绘制交互范围
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, npcData.interactionRange);
                
                // 绘制离开范围
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, npcData.interactionRange * 1.5f);
            }
        }
    }
}