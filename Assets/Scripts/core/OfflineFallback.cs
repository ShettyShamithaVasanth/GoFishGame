using UnityEngine;

public static class OfflineFallback
{
    public static void Enter()
    {
        GameModeManager.isOnlineMode = false;

        QuickMatchService.Instance?.Cancel();

        MenuController menu =
            Object.FindAnyObjectByType<MenuController>();

        if (menu == null)
        {
            Debug.LogError(
                "OfflineFallback: MenuController not found");
            return;
        }

        menu.ShowModeSelection("Offline");   
    }
}