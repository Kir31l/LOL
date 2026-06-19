using UnityEngine;

public class LobbyPlayerSetup : MonoBehaviour
{
    [System.Serializable]
    public struct CharacterOption
    {
        public Sprite idleSprite;
        public RuntimeAnimatorController animatorController;
    }

    [Header("Character Options (order must match menu)")]
    [SerializeField] private CharacterOption[] characters;

    private SpriteRenderer sr;
    private Animator anim;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
    }

    private void Start()
    {
        // Single-player fallback — multiplayer uses NetworkPlayer.ApplyCharacter instead
        if (GetComponent<NetworkPlayer>() == null)
            ApplyCharacter(PlayerSession.SelectedCharacter, PlayerSession.Username);
    }

    /// <summary>Called by NetworkPlayer to set character visuals and name tag.</summary>
    public void ApplyCharacter(int characterIndex, string username)
    {
        if (characters != null && characterIndex >= 0 && characterIndex < characters.Length)
        {
            CharacterOption c = characters[characterIndex];

            if (c.idleSprite != null && sr != null)
                sr.sprite = c.idleSprite;

            if (c.animatorController != null && anim != null)
                anim.runtimeAnimatorController = c.animatorController;
        }

        NameTag tag = GetComponentInChildren<NameTag>();
        if (tag != null)
            tag.UpdateName(username);
    }
}
