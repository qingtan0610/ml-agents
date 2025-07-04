using UnityEngine;
using AI.Config;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AI.Stats
{
    public static class AIStatsHelper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CheckForDefaultConfig()
        {
            // 尝试加载默认配置
            var defaultConfig = Resources.Load<AIStatsConfig>("DefaultAIStatsConfig");
            if (defaultConfig == null)
            {
                Debug.LogWarning("No DefaultAIStatsConfig found in Resources folder. Please create one!");
            }
        }
        
#if UNITY_EDITOR
        [MenuItem("Assets/Create/AI/Create Default Stats Config", false, 0)]
        static void CreateDefaultStatsConfig()
        {
            // 创建配置资源
            AIStatsConfig config = ScriptableObject.CreateInstance<AIStatsConfig>();
            
            // 设置默认值
            config.initialHealth = 100f;
            config.initialHunger = 100f;
            config.initialThirst = 100f;
            config.initialStamina = 100f;
            config.initialArmor = 0f;
            config.initialToughness = 50f;
            config.initialBullets = 30f;
            config.initialArrows = 20f;
            config.initialMana = 50f;
            
            // 创建资源文件夹
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            // 保存资源
            string path = "Assets/Resources/DefaultAIStatsConfig.asset";
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 选中创建的资源
            Selection.activeObject = config;
            
            Debug.Log($"Created DefaultAIStatsConfig at {path}");
        }
        
        [MenuItem("GameObject/AI/Setup AI Stats", false, 10)]
        static void SetupAIStats()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null) return;
            
            // 添加AIStats组件
            var aiStats = selected.GetComponent<AIStats>();
            if (aiStats == null)
            {
                aiStats = selected.AddComponent<AIStats>();
            }
            
            // 尝试自动分配配置
            if (aiStats.Config == null)
            {
                var config = Resources.Load<AIStatsConfig>("DefaultAIStatsConfig");
                if (config == null)
                {
                    // 如果没有默认配置，查找任何配置
                    string[] guids = AssetDatabase.FindAssets("t:AIStatsConfig");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        config = AssetDatabase.LoadAssetAtPath<AIStatsConfig>(path);
                    }
                }
                
                if (config != null)
                {
                    SerializedObject so = new SerializedObject(aiStats);
                    so.FindProperty("config").objectReferenceValue = config;
                    so.ApplyModifiedProperties();
                    Debug.Log($"Assigned {config.name} to AIStats component");
                }
                else
                {
                    Debug.LogWarning("No AIStatsConfig found. Please create one!");
                }
            }
        }
#endif
    }
}