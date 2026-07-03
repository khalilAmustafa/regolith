using UnityEngine;

#pragma warning disable 0618
public class RegolithPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private int amount = 1;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private float rotationSpeed = 30f;

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    public string GetPrompt(PlayerSurvivalResources player)
    {
        return $"Press E to collect {amount} regolith";
    }

    public bool CanInteract(PlayerSurvivalResources player)
    {
        return player != null && player.regolithAmount < player.maxRegolithAmount;
    }

    public void Interact(PlayerSurvivalResources player)
    {
        if (player == null)
        {
            return;
        }

        if (player.regolithAmount >= player.maxRegolithAmount)
        {
            if (SurvivalHUD.Instance != null)
            {
                SurvivalHUD.Instance.ShowMessage("Regolith storage full");
            }

            return;
        }

        player.AddRegolith(amount);

        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnRegolithPickedUp();
        }

        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
#pragma warning restore 0618
