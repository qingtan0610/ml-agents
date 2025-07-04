namespace Inventory.Interfaces
{
    public interface IStackable
    {
        int MaxStackSize { get; }
        bool IsStackable { get; }
    }
}