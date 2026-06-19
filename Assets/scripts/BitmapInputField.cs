using UnityEngine;

public class BitmapInputField : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BitmapText bitmapText;
    [SerializeField] private BitmapText placeholderText;

    [Header("Settings")]
    [SerializeField] private int maxCharacters = 12;
    [SerializeField] private string placeholder = "USERNAME";
    [SerializeField] private string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private string text = "";
    private bool isFocused;
    private float cursorBlink;
    private bool showCursor;

    void Start()
    {
        if (bitmapText == null)
            bitmapText = GetComponent<BitmapText>();

        if (placeholderText != null)
            placeholderText.SetText(placeholder);

        if (bitmapText != null)
            bitmapText.SetText("");
    }

    void OnMouseDown()
    {
        isFocused = true;
        cursorBlink = 0f;
        showCursor = true;
        UpdateDisplay();
    }

    void Update()
    {
        // Click anywhere else to unfocus
        if (isFocused && Input.GetMouseButtonDown(0))
        {
            // Check if we clicked on this object
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(mousePos);
            if (hit == null || hit.gameObject != gameObject)
            {
                isFocused = false;
                UpdateDisplay();
                return;
            }
        }

        if (!isFocused)
            return;

        // Cursor blink
        cursorBlink += Time.deltaTime;
        if (cursorBlink >= 0.5f)
        {
            cursorBlink = 0f;
            showCursor = !showCursor;
            UpdateDisplay();
        }

        // Handle input
        foreach (char c in Input.inputString)
        {
            if (c == '\b') // Backspace
            {
                if (text.Length > 0)
                    text = text.Substring(0, text.Length - 1);
            }
            else if (c == '\n' || c == '\r') // Enter
            {
                isFocused = false;
                if (!string.IsNullOrEmpty(text))
                    PlayerSession.Username = text;
            }
            else if (text.Length < maxCharacters)
            {
                char upper = char.ToUpperInvariant(c);
                if (allowedChars.IndexOf(upper) >= 0)
                    text += upper;
            }

            cursorBlink = 0f;
            showCursor = true;
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (bitmapText == null) return;

        if (isFocused)
        {
            string display = text;
            if (showCursor)
                display += "|";
            bitmapText.SetText(display);
        }
        else if (string.IsNullOrEmpty(text))
        {
            bitmapText.SetText("");
        }
        else
        {
            bitmapText.SetText(text);
        }
    }

    public string GetText() => text;

    public void Clear()
    {
        text = "";
        isFocused = false;
        UpdateDisplay();
    }
}
