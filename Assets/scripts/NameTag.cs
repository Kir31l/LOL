using UnityEngine;

public class NameTag : MonoBehaviour
{
    [Header("Position")]
    [SerializeField] private float offsetY = 1.5f;

    private BitmapText nameText;

    private void Start()
    {
        nameText = GetComponent<BitmapText>();

        // Position above the player (local offset since we're a child)
        transform.localPosition = new Vector3(0f, offsetY, 0f);

        // NOTE: Do NOT set the name from PlayerSession.Username here.
        // That static value is LOCAL to each peer — on the client it would
        // overwrite every player's name tag with the client's own username.
        // NetworkPlayer.Render() → ApplyCharacterState() handles correct
        // per-player name sync via [Networked] UsernameNV.
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
