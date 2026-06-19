using UnityEngine;

public class NameTag : MonoBehaviour
{
    [Header("Position")]
    [SerializeField] private float offsetY = 1.5f;

    private BitmapText nameText;

    private void Start()
    {
        nameText = GetComponent<BitmapText>();
        UpdateName(PlayerSession.Username);

        // Position above the player (local offset since we're a child)
        transform.localPosition = new Vector3(0f, offsetY, 0f);
    }

    private void LateUpdate()
    {
        // Counteract parent's flip so text stays readable
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Sign(transform.parent.localScale.x);
        transform.localScale = scale;
    }

    public void UpdateName(string newName)
    {
        if (nameText == null)
            nameText = GetComponent<BitmapText>();

        if (nameText != null)
            nameText.SetText(newName);
    }
}
