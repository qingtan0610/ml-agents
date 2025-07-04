using UnityEngine;
using System.Collections.Generic;

namespace NPC.Data
{
    [CreateAssetMenu(fileName = "NPCData", menuName = "NPC/Base NPC Data")]
    public class NPCData : ScriptableObject
    {
        [Header("Basic Info")]
        public string npcId;
        public string npcName;
        public NPCType npcType;
        public NPCInteractionType interactionType;
        
        [Header("Visual")]
        public Sprite npcSprite;
        public Color npcColor = Color.white;
        public GameObject npcModelPrefab;
        
        [Header("Behavior")]
        public float interactionRange = 3f;
        public NPCMood defaultMood = NPCMood.Neutral;
        public bool canMove = false;
        public float moveSpeed = 0f;
        
        [Header("Dialogue")]
        [TextArea(2, 4)]
        public string greetingText = "你好，冒险者！";
        [TextArea(2, 4)]
        public string farewellText = "再见！";
        [TextArea(2, 4)]
        public string busyText = "请稍等，我正在忙...";
        
        [Header("Audio")]
        public AudioClip greetingSound;
        public AudioClip interactionSound;
        public AudioClip farewellSound;
        
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(npcId))
            {
                npcId = name.ToLower().Replace(" ", "_");
            }
        }
    }
    
    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea(2, 4)]
        public string text;
        public float displayTime = 3f;
        public AudioClip voiceClip;
    }
}