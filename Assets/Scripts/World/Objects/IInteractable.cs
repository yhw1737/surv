namespace Isle.World.Objects
{
    /// <summary>Something a player's reach check can act on (see
    /// <c>Isle.Gameplay.Character.PlayerInteraction</c>). Server-only call site — Absolute Rule 2,
    /// state changes happen server-side only.</summary>
    public interface IInteractable
    {
        void Interact();
    }
}
