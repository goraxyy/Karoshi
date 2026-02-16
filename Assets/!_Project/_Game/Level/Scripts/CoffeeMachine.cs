using UnityEngine;

public class CoffeeMachine : MonoBehaviour, IInteractable
{
    public float cooldownTime = 15f;

    float lastUseTime = -999f;

    public void Interact(PlayerInteract player)
    {
        if (Time.time - lastUseTime < cooldownTime)
        {
            Debug.Log("Coffee machine on cooldown!");
            return;
        }

        BurnoutSystem burnout = FindFirstObjectByType<BurnoutSystem>();
        if (burnout != null)
        {
            burnout.DrinkCoffee();
            lastUseTime = Time.time;
        }
    }

    public string GetPrompt()
    {
        if (Time.time - lastUseTime < cooldownTime)
            return $"Cooldown: {(int)(cooldownTime - (Time.time - lastUseTime))}s";

        return "Drink Coffee [E]";
    }
}