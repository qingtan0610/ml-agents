using UnityEngine;

namespace NPC.Interfaces
{
    public interface INPCInteractable
    {
        NPCType NPCType { get; }
        NPCInteractionType InteractionType { get; }
        bool CanInteract(GameObject interactor);
        void StartInteraction(GameObject interactor);
        void EndInteraction();
        string GetInteractionPrompt();
    }
    
    public interface IShopkeeper
    {
        void OpenShop(GameObject customer);
        bool CanAfford(GameObject customer, string itemId, int quantity);
        bool PurchaseItem(GameObject customer, string itemId, int quantity);
        float GetPriceMultiplier();
    }
    
    public interface IServiceProvider
    {
        void ProvideService(GameObject customer, string serviceId);
        int GetServiceCost(string serviceId);
        bool CanProvideService(GameObject customer, string serviceId);
        string GetServiceDescription(string serviceId);
    }
    
    public interface ICrafter
    {
        void OpenCraftingMenu(GameObject customer);
        bool CanCraft(GameObject customer, string recipeId);
        bool CraftItem(GameObject customer, string recipeId);
        bool UpgradeItem(GameObject customer, string itemId);
    }
}