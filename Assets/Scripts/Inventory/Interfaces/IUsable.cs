using UnityEngine;

namespace Inventory.Interfaces
{
    public interface IUsable
    {
        bool CanUse(GameObject user);
        void Use(GameObject user);
        float UseTime { get; }
        string UseAnimation { get; }
    }
}