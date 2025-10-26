namespace Game
{
    public interface IHandItemBehavior
    {
        void OnEquip();
        void OnUnequip();
        void OnUsePressed();
        void OnUseReleased();
        void OnUseHeld(float delta);
        void OnMuzzleFlash();
    }
}
