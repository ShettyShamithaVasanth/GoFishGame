# Fix: Null Guard on LoadingPanel in MenuController.Start()

## Error
```
UnassignedReferenceException: The variable LoadingPanel of MenuController has not been assigned.
```
Line 49: `LoadingPanel.SetActive(true)` — no null check.

## Root Cause
The previous plan's code didn't add null guards on `LoadingPanel`, `MenuBackground`, and `MenuUI` in `Start()` and `ShowMenuAfterLoading()`. Every other method in the file (e.g., `ShowModeSelection`, `ContinueGame`) already uses null guards — `Start()` and the new coroutine must follow the same pattern.

## File to Modify
`Assets/Scripts/MenuController.cs`

## Two Options

### Option A: Assign LoadingPanel in Unity Inspector (RECOMMENDED)
1. In Unity, select the GameObject that has the `MenuController` script
2. In the Inspector, find the `LoadingPanel` field (currently None/Empty)
3. Drag the LoadingPanel GameObject from the Hierarchy into that field
4. Do the same for any other unassigned fields (`MenuUI`, `MenuBackground`, `ModeSelectionPanel`)

This is the proper fix — the field exists for a reason.

### Option B: Add null guards in code (belt-and-suspenders)
Replace `Start()` and `ShowMenuAfterLoading()` with null-safe versions:

```csharp
void Start()
{
    if (ModeSelectionPanel != null)
        ModeSelectionPanel.SetActive(false);

    if (GameOverUI.skipMenuOnReload)
    {
        GameOverUI.skipMenuOnReload = false;
        if (MenuUI != null) MenuUI.SetActive(false);
        if (LoadingPanel != null) LoadingPanel.SetActive(false);
        if (MenuBackground != null) MenuBackground.SetActive(false);
        GameManager.SetActive(true);
        if (gameSceneUI != null)
            gameSceneUI.ShowPanel();
        return;
    }

    if (MenuBackground != null) MenuBackground.SetActive(false);
    if (LoadingPanel != null) LoadingPanel.SetActive(true);
    if (MenuUI != null) MenuUI.SetActive(false);

    StartCoroutine(ShowMenuAfterLoading());
}

IEnumerator ShowMenuAfterLoading()
{
    yield return new WaitForSeconds(2f);

    if (LoadingPanel != null) LoadingPanel.SetActive(false);
    if (MenuBackground != null) MenuBackground.SetActive(true);
    if (MenuUI != null) MenuUI.SetActive(true);

    if (menuAnimationController != null)
        menuAnimationController.StartAnimations();
}
```

## Recommendation
Do **both** Option A and Option B. Option A is the real fix (assign the reference). Option B prevents future crashes if the reference is accidentally cleared.
