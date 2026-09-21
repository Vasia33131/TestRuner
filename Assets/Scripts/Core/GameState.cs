namespace ButchersGames.Core
{
    public enum GameState
    {
        /// <summary>The level is loaded, the player waits for the start button.</summary>
        Ready,
        /// <summary>The player is running.</summary>
        Playing,
        /// <summary>The finish was reached, the result screen is shown.</summary>
        Won
    }
}
