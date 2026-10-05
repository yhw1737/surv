namespace Isle.Gameplay.Character
{
    /// <summary>
    /// Set by the HUD when the mouse is over a panel or busy placing a structure, so a click there doesn't also
    /// swing the weapon. Presentation-to-input only; never consulted by server logic.
    /// </summary>
    public static class PointerGate
    {
        public static bool Captured { get; set; }
    }
}
