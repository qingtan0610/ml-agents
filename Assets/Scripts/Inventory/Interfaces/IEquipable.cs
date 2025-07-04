using UnityEngine;

namespace Inventory.Interfaces
{
    public interface IEquipable
    {
        EquipmentSlot Slot { get; }
        bool CanEquip(GameObject user);
        void OnEquip(GameObject user);
        void OnUnequip(GameObject user);
    }
}