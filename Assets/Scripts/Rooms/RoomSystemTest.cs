using UnityEngine;

namespace Rooms
{
    /// <summary>
    /// 房间系统测试脚本
    /// </summary>
    public class RoomSystemTest : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool autoGenerateOnStart = true;
        
        private MapGenerator mapGenerator;
        
        private void Start()
        {
            mapGenerator = GetComponent<MapGenerator>();
            if (mapGenerator == null)
            {
                mapGenerator = gameObject.AddComponent<MapGenerator>();
            }
            
            if (autoGenerateOnStart)
            {
                Debug.Log("[RoomSystemTest] Starting map generation test...");
                // MapGenerator会在自己的Start中生成地图
            }
        }
        
        private void Update()
        {
            // 测试按键
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Debug.Log("[RoomSystemTest] Regenerating map...");
                mapGenerator.GenerateMap();
            }
            
            if (Input.GetKeyDown(KeyCode.F6))
            {
                Debug.Log("[RoomSystemTest] Teleporting to next level...");
                mapGenerator.TeleportToNextLevel();
            }
        }
        
        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 300, 20), "Room System Test");
            GUI.Label(new Rect(10, 30, 300, 20), "F5 - Regenerate Map");
            GUI.Label(new Rect(10, 50, 300, 20), "F6 - Next Level");
        }
    }
}