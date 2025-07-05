using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NPC.Data;

namespace NPC.Runtime
{
    /// <summary>
    /// 医生运行时服务 - 每个医生独立的服务列表
    /// </summary>
    [System.Serializable]
    public class RuntimeDoctorServices
    {
        private List<MedicalService> availableServices = new List<MedicalService>();
        private RuntimeShopInventory medicineInventory;
        
        public RuntimeDoctorServices()
        {
            medicineInventory = new RuntimeShopInventory();
        }
        
        /// <summary>
        /// 初始化服务（全部）
        /// </summary>
        public void Initialize(DoctorData doctorData)
        {
            availableServices.Clear();
            
            if (doctorData.services != null)
            {
                availableServices.AddRange(doctorData.services);
            }
            
            if (doctorData.medicineShop != null)
            {
                medicineInventory.Initialize(doctorData.medicineShop);
            }
        }
        
        /// <summary>
        /// 初始化服务（随机选择）
        /// </summary>
        public void InitializeRandomized(DoctorData doctorData, float seed)
        {
            availableServices.Clear();
            
            // 随机选择医疗服务
            if (doctorData.randomizeServices && doctorData.services != null && doctorData.services.Count > 0)
            {
                int serviceCount = ServiceRandomizer.GetRandomCount(
                    doctorData.minServices, 
                    doctorData.maxServices, 
                    seed
                );
                
                availableServices = ServiceRandomizer.RandomSelect(
                    doctorData.services, 
                    serviceCount, 
                    seed
                );
                
                Debug.Log($"[RuntimeDoctorServices] 随机选择了 {availableServices.Count} 种医疗服务");
            }
            else
            {
                // 使用全部服务
                if (doctorData.services != null)
                {
                    availableServices.AddRange(doctorData.services);
                }
            }
            
            // 随机选择药品
            if (doctorData.randomizeMedicine && doctorData.medicineShop != null)
            {
                int medicineCount = ServiceRandomizer.GetRandomCount(
                    doctorData.minMedicineTypes,
                    doctorData.maxMedicineTypes,
                    seed + 1000f // 使用不同的种子
                );
                
                medicineInventory.InitializeRandomized(doctorData.medicineShop, medicineCount, seed + 1000f);
                
                Debug.Log($"[RuntimeDoctorServices] 随机选择了 {medicineCount} 种药品");
            }
            else
            {
                // 使用全部药品
                if (doctorData.medicineShop != null)
                {
                    medicineInventory.Initialize(doctorData.medicineShop);
                }
            }
        }
        
        /// <summary>
        /// 获取所有可用服务
        /// </summary>
        public List<MedicalService> GetAvailableServices()
        {
            return new List<MedicalService>(availableServices);
        }
        
        /// <summary>
        /// 获取特定服务
        /// </summary>
        public MedicalService GetService(string serviceName)
        {
            return availableServices.Find(s => s.serviceName == serviceName);
        }
        
        /// <summary>
        /// 获取药品库存
        /// </summary>
        public RuntimeShopInventory GetMedicineInventory()
        {
            return medicineInventory;
        }
        
        /// <summary>
        /// 保存数据
        /// </summary>
        public DoctorServicesSaveData GetSaveData()
        {
            return new DoctorServicesSaveData
            {
                availableServiceNames = availableServices.Select(s => s.serviceName).ToList(),
                medicineInventory = medicineInventory.GetSaveData()
            };
        }
        
        /// <summary>
        /// 加载数据
        /// </summary>
        public void LoadSaveData(DoctorServicesSaveData saveData, DoctorData doctorData)
        {
            if (saveData == null) return;
            
            // 恢复服务列表
            availableServices.Clear();
            if (saveData.availableServiceNames != null && doctorData.services != null)
            {
                foreach (var serviceName in saveData.availableServiceNames)
                {
                    var service = doctorData.services.Find(s => s.serviceName == serviceName);
                    if (service != null)
                    {
                        availableServices.Add(service);
                    }
                }
            }
            
            // 恢复药品库存
            if (saveData.medicineInventory != null)
            {
                medicineInventory.LoadSaveData(saveData.medicineInventory);
            }
        }
    }
    
    /// <summary>
    /// 医生服务存档数据
    /// </summary>
    [System.Serializable]
    public class DoctorServicesSaveData
    {
        public List<string> availableServiceNames;
        public ShopInventorySaveData medicineInventory;
    }
}