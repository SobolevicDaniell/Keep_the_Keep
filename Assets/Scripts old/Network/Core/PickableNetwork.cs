using Fusion;

public class PickableNetwork : NetworkBehaviour
{
    [Networked] public string ItemId { get; private set; }
    [Networked] public int Amount { get; private set; }
    [Networked] public int Ammo { get; private set; }

    public void Init(string itemId, int amount, int ammo)
    {
        ItemId = itemId;
        Amount = amount;
        Ammo = ammo;
    }
}
