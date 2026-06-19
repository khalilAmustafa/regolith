using UnityEngine;

public class RegolithPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private int amount = 1;
    [SerializeField] private bool destroyOnPickup = true;
    [SerializeField] private float rotationSpeed = 30f;

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
    }

    public string GetPrompt()
    {
        return $"Press E to collect {amount} regolith";
    }

    public void Interact(GameObject player)
    {
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            return;
        }

        inventory.AddRegolith(amount);

        GameManager gameManager = FindObjectOfType<GameManager>();
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
