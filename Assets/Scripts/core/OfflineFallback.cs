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

        if (menu.MenuUI != null)
            menu.MenuUI.SetActive(false);

        if (menu.LoadingPanel != null)
            menu.LoadingPanel.SetActive(false);
        if (menu.MenuBackground != null)
            menu.MenuBackground.SetActive(false);

        if (menu.ModeSelectionPanel != null)
        {
            menu.ModeSelectionPanel.SetActive(true);

            ModeSelectionController controller =
                menu.ModeSelectionPanel
                    .GetComponent<ModeSelectionController>();

            if (controller != null)
            {
                controller.SetHeader("Offline");
            }
        }
    }
}