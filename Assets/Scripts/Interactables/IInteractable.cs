using UnityEngine;

namespace Interactables
{
    /// <summary>
    /// 通用交互接口
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 与对象交互
        /// </summary>
        /// <param name="interactor">交互者</param>
        void Interact(GameObject interactor);
    }
}