using UnityEngine;
using AI.Stats;

namespace Interactables
{
    /// <summary>
    /// 泉水交互对象，可以补充水分
    /// </summary>
    public class Fountain : MonoBehaviour, IInteractable
    {
        [Header("Fountain Settings")]
        [SerializeField] private float waterRestoreAmount = 50f; // 恢复的水分量
        [SerializeField] private float interactionRange = 2f;
        [SerializeField] private float drinkDuration = 2f; // 喝水动画时长
        [SerializeField] private float cooldownTime = 1f; // 冷却时间
        
        [Header("Visual")]
        [SerializeField] private GameObject fountainVisual;
        [SerializeField] private ParticleSystem waterParticles;
        [SerializeField] private AudioSource waterSound;
        
        [Header("UI")]
        [SerializeField] private GameObject interactionPrompt;
        [SerializeField] private string promptText = "按E饮水";
        
        private bool canInteract = true;
        private float cooldownTimer = 0f;
        
        
        private void Start()
        {
            if (interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
            
            // 启动水流粒子效果
            if (waterParticles != null)
            {
                waterParticles.Play();
            }
            
            // 播放水声
            if (waterSound != null)
            {
                waterSound.loop = true;
                waterSound.Play();
            }
        }
        
        private void Update()
        {
            if (!canInteract)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0)
                {
                    canInteract = true;
                }
            }
        }
        
        public void Interact(GameObject interactor)
        {
            if (!canInteract) return;
            
            var aiStats = interactor.GetComponent<AIStats>();
            if (aiStats == null)
            {
                Debug.LogWarning("[Fountain] Interactor has no AIStats component!");
                return;
            }
            
            // 检查是否需要补水
            var currentThirst = aiStats.GetStat(StatType.Thirst);
            var maxThirst = aiStats.Config?.maxThirst ?? 100f;
            
            if (currentThirst >= maxThirst)
            {
                Debug.Log("[Fountain] AI is not thirsty!");
                return;
            }
            
            // 开始喝水
            StartCoroutine(DrinkWater(aiStats));
        }
        
        private System.Collections.IEnumerator DrinkWater(AIStats aiStats)
        {
            canInteract = false;
            cooldownTimer = cooldownTime;
            
            Debug.Log($"[Fountain] {aiStats.name} starts drinking water...");
            
            // TODO: 播放喝水动画
            
            // 增强水流效果
            if (waterParticles != null)
            {
                var emission = waterParticles.emission;
                emission.rateOverTime = emission.rateOverTime.constant * 2f;
            }
            
            yield return new WaitForSeconds(drinkDuration);
            
            // 恢复水分
            aiStats.ModifyStat(StatType.Thirst, waterRestoreAmount);
            
            // 恢复粒子效果
            if (waterParticles != null)
            {
                var emission = waterParticles.emission;
                emission.rateOverTime = emission.rateOverTime.constant / 2f;
            }
            
            Debug.Log($"[Fountain] {aiStats.name} restored {waterRestoreAmount} thirst!");
        }
        
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") && interactionPrompt != null && canInteract)
            {
                interactionPrompt.SetActive(true);
            }
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player") && interactionPrompt != null)
            {
                interactionPrompt.SetActive(false);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}