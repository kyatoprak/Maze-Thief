using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ClosetScript : MonoBehaviour
{
    [Header("Closet Settings")]
    public Sprite closedSprite;
    public Sprite openSprite;
    public float hideDuration = 5f; 
    public float exitDelay = 0.5f;

    [Header("UI Elements")]
    public GameObject indicatorText; 

    [Header("Audio (Optional)")]
    public AudioClip openSound;
    public AudioClip closeSound;

    private SpriteRenderer spriteRenderer;
    private AudioSource audioSource;
    private bool isHiding = false;
    private GameObject player;
    private Vector3 playerOriginalPosition;
    private float originalMoveSpeed;
    private gameManager GameManager;

    private bool playerInRange = false;

    public bool IsPlayerInRange => playerInRange;
    public bool IsHiding => isHiding;

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        GameManager = GameObject.FindGameObjectWithTag("GameController").GetComponent<gameManager>();

        if (openSprite != null && spriteRenderer != null)
            spriteRenderer.sprite = openSprite;

        if (GameManager != null)
        {
            GameManager.RegisterCloset(this);
        }
        
    }

    public void ToggleCloset()
    {
        if (isHiding)
        {
            ExitCloset();
        }
        else
        {
            if (player != null && GameManager != null)
            {
                StartCoroutine(EnterCloset());
            }
            
        }
    }

    private IEnumerator EnterCloset()
    {
        if (isHiding || player == null) yield break;

        if (!playerInRange)
        {
            yield break;
        }

        var playerScript = player.GetComponent<playerScript>();
        if (playerScript == null) yield break;

        playerScript.isInCloset = true;
        GameManager.NotifyPlayerInCloset(true);

        isHiding = true;
        playerOriginalPosition = player.transform.position;
        originalMoveSpeed = playerScript.moveSpeed;
        playerScript.moveSpeed = 0f;
        player.GetComponent<SpriteRenderer>().enabled = false;

        if (closedSprite != null) spriteRenderer.sprite = closedSprite;
        if (closeSound != null) audioSource.PlayOneShot(closeSound);

        playerScript.heartbeatActive = false;
        playerScript.screenEffectActive = false;

        if (indicatorText != null)
        {
            indicatorText.SetActive(false);
        }

 
    }

    public void ExitCloset()
    {
        if (!isHiding || player == null) return;
        var playerScript = player.GetComponent<playerScript>();
        if (playerScript == null) return;

        playerScript.isInCloset = false;
        GameManager.NotifyPlayerInCloset(false);

        isHiding = false;
        player.GetComponent<SpriteRenderer>().enabled = true;
        player.transform.position = playerOriginalPosition;
        StartCoroutine(EnableMovementAfterDelay(playerScript, exitDelay));

        if (openSprite != null) spriteRenderer.sprite = openSprite;
        if (openSound != null) audioSource.PlayOneShot(openSound);

        if (indicatorText != null)
        {
            indicatorText.SetActive(true);
        }
    }

    private IEnumerator EnableMovementAfterDelay(playerScript playerScript, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (playerScript != null && GameManager.GameIsActive && !GameManager.eyeIsActive)
            playerScript.moveSpeed = originalMoveSpeed;
    }

    public void ForceExit()
    {
        if (isHiding) ExitCloset();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            player = other.gameObject;
            playerInRange = true;

            if (GameManager != null)
            {
                GameManager.SetActiveCloset(this);
            }
           

            if (indicatorText != null)
            {
                indicatorText.SetActive(true);
            }
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            player = null;

            if (isHiding)
            {
                ExitCloset();
            }

            if (GameManager != null)
            {
                GameManager.ClearActiveCloset(this);
            }

            if (indicatorText != null)
            {
                indicatorText.SetActive(false);
            }
        }
    }
}