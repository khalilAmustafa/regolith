public interface IInteractable
{
    string GetPrompt(PlayerSurvivalResources player);
    bool CanInteract(PlayerSurvivalResources player);
    void Interact(PlayerSurvivalResources player);
}
