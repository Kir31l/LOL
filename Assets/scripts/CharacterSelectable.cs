using UnityEngine;

public class CharacterSelectable : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color selectedTint = Color.white;
    [SerializeField] private Color unselectedTint = new Color(0.5f, 0.5f, 0.5f, 1f);
    [SerializeField] private float selectedScale = 1.15f;

    [Header("Selection")]
    [SerializeField] private int characterIndex;

    private SpriteRenderer sr;
    private Vector3 originalScale;
    private static CharacterSelectable currentSelected;

    public int CharacterIndex => characterIndex;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale;
    }

    void OnMouseDown()
    {
        // Deselect previous
        if (currentSelected != null && currentSelected != this)
            currentSelected.SetSelected(false);

        // Toggle this one
        bool willBeSelected = currentSelected != this;
        SetSelected(willBeSelected);
        currentSelected = willBeSelected ? this : null;

        // Save selection
        if (willBeSelected)
            PlayerSession.SelectedCharacter = characterIndex;
    }

    private void SetSelected(bool selected)
    {
        if (sr != null)
            sr.color = selected ? selectedTint : unselectedTint;

        transform.localScale = selected
            ? originalScale * selectedScale
            : originalScale;
    }

    public bool IsSelected() => currentSelected == this;

    public static CharacterSelectable GetSelected() => currentSelected;

    public static void ClearSelection()
    {
        if (currentSelected != null)
        {
            currentSelected.SetSelected(false);
            currentSelected = null;
        }
    }
}
