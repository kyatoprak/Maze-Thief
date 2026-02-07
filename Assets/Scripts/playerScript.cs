using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class playerScript : MonoBehaviour
{
    [Header("UI")]
    public GameObject healEffect;
    
    public int requiredTreasuresForEye = 1;
    private int totalTreasuresInLevel = 0; 
    public GameObject eyeButton; 
    public TMP_Text eyeCounterText; 
    
    
    
    [Header("Heartbeat System")]
    public AudioSource heartbeatAudioSource;
    public AudioClip heartbeatClip;
    public float minInterval = 0.3f;
    public float maxInterval = 1.5f;
    public float minVolume = 0.2f;
    public float maxVolume = 1.0f;
    public bool isInCloset = false; 

    [Header("Screen Darkening Effect")]
    public Image screenOverlay;
    public Color nearGhostColor = new Color(0f, 0f, 0f, 0.7f);
    public Color extremeGhostColor = new Color(0.3f, 0f, 0f, 0.9f);
    public float fadeSpeed = 3f;

    [Header("Threat Overlay Effect")]
    public Image threatOverlay; 
    public Color flashColor = new Color(1f, 0f, 0f, 1f); 
    [Range(0.5f, 8f)] public float flashPulseSpeed = 4f;  
    [Range(0f, 1f)] public float flashMaxIntensity = 0.8f;   

    
    [Header("Threat UI Debounce")]
    public float threatOnThreshold = 0.70f;
    public float threatOffThreshold = 0.68f;
    public float threatRearmDelay = 2f;

    private bool threatVisualOn = false;  
    private float threatRearmUntil = 0f;   

    
    
    [Header("Scene Objects")]
    public GameObject chest;
    public GameObject key;
    public GameObject pressEWarning;
    public GameObject youHaveToFindTheKeyWarning;
    public gameManager GameManager;

    [Header("Game Settings")]
    public InputActionReference moveActionToUse;
    
    private Rigidbody2D rb;
    private Animator animator;
    private Camera mainCamera;
    private Vector3 velocity = Vector3.zero;

    [SerializeField] private float damping = 0.1f;
    [SerializeField] public float moveSpeed = 3f;
    [SerializeField] private float interactDistance = 1.5f;

    private int collectedTreasures = 0;
    private float sceneStartTime;
    private int playerHeal = 100;
    private int playerScore = 0;
    private bool eyeIsActive = false;
    private bool playerFoundTheKey = false;
    private bool playerInTheDoorOpenZone = false;

    
    public int eyeCount = 0; 
    public int maxEyeCount = 0; 
    
    
    // AI ve efektle
    public bool isHidden = false;
    private GameObject ghost;
    private AStarAI ghostAI;
    private float nextHeartbeatTime = 0f;
    public bool heartbeatActive = false;
    public bool screenEffectActive = false;
    private Color targetOverlayColor = Color.clear;

    private BoxCollider2D doorCollider;
    private CircleCollider2D doorTrigger;

    private List<SpriteRenderer> tempRenderers = new();

    void Awake()
    {
        
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;
    }

    
    
    void Start()
    {
        
        if (GameManager == null)
            GameManager = GameObject.FindGameObjectWithTag("GameController")?.GetComponent<gameManager>();

        if (GameManager == null)
        {
            return;
        }

        var doorObj = GameObject.FindGameObjectWithTag("Door");
        var chestObj = GameObject.FindGameObjectWithTag("Chest");
        var keyObj = GameObject.FindGameObjectWithTag("Key");

        if (doorObj != null)
        {
            doorCollider = doorObj.GetComponentInChildren<BoxCollider2D>();
            doorTrigger = doorObj.GetComponentInChildren<CircleCollider2D>();
        }

        if (chestObj != null) chest = chestObj;
        if (keyObj != null) key = keyObj;

        if (key != null) key.GetComponent<SpriteRenderer>().enabled = false;

        sceneStartTime = Time.time;

        ghost = GameObject.FindGameObjectWithTag("Enemy");
        if (ghost != null)
        {
            ghostAI = ghost.GetComponent<AStarAI>();
        }

        if (heartbeatAudioSource == null)
        {
            heartbeatAudioSource = gameObject.AddComponent<AudioSource>();
        }
        heartbeatAudioSource.clip = heartbeatClip;
        heartbeatAudioSource.loop = false;
        heartbeatAudioSource.playOnAwake = false;

        if (screenOverlay != null)
        {
            screenOverlay.color = Color.clear;
        }
        

        if (eyeButton == null)
        {
            eyeButton = GameObject.FindGameObjectWithTag("EyeButton");
        }

        if (eyeCounterText == null && eyeButton != null)
        {
            eyeCounterText = eyeButton.GetComponentInChildren<TMP_Text>();
            
        }
        
        maxEyeCount = GameManager.totalTreasuresInLevel;
        eyeCount = 0; 

        totalTreasuresInLevel = GameManager.Level switch
        {
            <= 10  => 1,
            <= 25 => 3,
            _     => 5
        };
        if (eyeButton == null)
        {
            eyeButton = GameObject.FindGameObjectWithTag("EyeButton");
            if (eyeButton == null)
            {
                return;
            }
        }

        AssignEyeButton();
        UpdateEyeUI();
        
    }
    
    

    
    [ContextMenu("Reset Eye Count")]
    private void ResetEyeCount()
    {
        PlayerPrefs.DeleteKey("EyeCount");
    }
  
  
    private void UpdateEyeUI()
    {
        if (eyeCounterText != null)
        {
            eyeCounterText.text = $"x{eyeCount}";
        }

        if (eyeButton != null)
        {
            eyeButton.SetActive(eyeCount > 0);
        }
    }
    public void OnEyeButtonClick()
    {
        UseEye();
    }
    
    public void OnTreasureCollected()
    { GameManager?.UpdateTreasureCounter(1);
    }
    public void UseEye()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (GameManager == null)
        {
            GameManager = GameObject.FindGameObjectWithTag("GameController")?.GetComponent<gameManager>();
            if (GameManager == null)
            {
                return;
            }
        }

        if (eyeCount <= 0)
        {
            return;
        }

        if (GameManager.eyeIsActive)
        {
            return;
        }

        if (!GameManager.GameIsActive)
        {
            return;
        }

        eyeCount--;
        UpdateEyeUI();

        GameManager.eyeIsActive = true;
        StartCoroutine(EyeEffectCoroutine());
    }
    private void CollectTreasure(Collider2D treasure)
    {
        if (treasure == null) return;

        var sr = treasure.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;

        treasure.GetComponent<Collider2D>().enabled = false;

        if (GameManager.GameObjects.Contains(treasure.gameObject))
        {
            GameManager.GameObjects.Remove(treasure.gameObject);
        }

        if (GameManager.treasures.Contains(treasure.gameObject))
        {
            GameManager.treasures.Remove(treasure.gameObject); 
        }
        GameManager?.UpdateTreasureCounter(1);

        collectedTreasures++;

        Destroy(treasure.gameObject, 1.3f);

        if (eyeCount < maxEyeCount)
        {
            eyeCount++;
            UpdateEyeUI();
        }
    }
    


    
    private IEnumerator EyeEffectCoroutine()
    {

        GameManager.audioSourca.PlayOneShot(GameManager.EyeAudioClip);
        moveSpeed = 0;

        if (key != null) key.GetComponent<SpriteRenderer>().enabled = false;
        foreach (var obj in GameManager.GameObjects)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = true;
        }
        foreach (var obj in GameManager.treasures)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = true;
        }

        yield return new WaitForSeconds(6f);

        GameManager.eyeIsActive = false;
        moveSpeed = 3f;

        if (key != null) key.GetComponent<SpriteRenderer>().enabled = true;
        foreach (var obj in GameManager.GameObjects)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
        foreach (var obj in GameManager.treasures)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
    }
    void Update()
    {
        
        
        
        if (playerInTheDoorOpenZone && Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryOpenDoor();
        }

        HandleThreatEffect();

        if (screenEffectActive && screenOverlay != null && screenOverlay.color.a > 0.01f)
        {
            screenOverlay.color = Color.Lerp(screenOverlay.color, targetOverlayColor, Time.deltaTime * fadeSpeed);
        }
        else if (!screenEffectActive && screenOverlay != null)
        {
            screenOverlay.color = Color.Lerp(screenOverlay.color, Color.clear, Time.deltaTime * fadeSpeed);
        }
    }

    private void HandleThreatEffect()
    {
        if (!GameManager || !GameManager.GameIsActive || GameManager.eyeIsActive)
        {
            heartbeatActive = false;
            screenEffectActive = false;
            UpdateThreatOverlay(0f);
            return;
        }

        if (isInCloset)
        {
            heartbeatActive = false;
            screenEffectActive = false;
            UpdateThreatOverlay(0f);
            return;
        }
        int minPathLength = 999;

        foreach (var ai in GameManager.ghosts)
        {
            if (ai == null || ai.path == null) continue;
            if (ai.path.Count < minPathLength)
            {
                minPathLength = ai.path.Count;
            }
        }

        if (minPathLength >= 999)
        {
            heartbeatActive = false;
            screenEffectActive = false;
            UpdateThreatOverlay(0f);
            return;
        }

        int maxThreatDistance = GameManager.Level switch
        {
            1 => 10,
            2 => 15,
            3 => 20,
            _ => 20
        };

        float t = Mathf.Clamp01((maxThreatDistance - minPathLength) / (maxThreatDistance - 1f));

        // Kalp atışı, ses, ekran efekti
        float interval = Mathf.Lerp(maxInterval, minInterval, t);
        float volume = Mathf.Lerp(minVolume, maxVolume, t);
        heartbeatAudioSource.volume = volume;

        if (Time.time >= nextHeartbeatTime)
        {
            heartbeatAudioSource.PlayOneShot(heartbeatClip);
            nextHeartbeatTime = Time.time + interval;
        }

        float pulse = Mathf.PingPong(Time.time * flashPulseSpeed, 1f);
        float desiredIntensity = pulse * flashMaxIntensity;

        DebouncedThreatUI(t, desiredIntensity);

        heartbeatActive = true;

        if (threatVisualOn)
        {
            screenEffectActive = true;
            targetOverlayColor = Color.Lerp(nearGhostColor, extremeGhostColor, t);
        }
        else
        {
            screenEffectActive = false;
            targetOverlayColor = Color.clear;
        }
    }
 
    private void UpdateThreatOverlay(float intensity)
    {
        if (threatOverlay == null) return;

        Color currentColor = flashColor;
        currentColor.a = intensity;
        threatOverlay.color = currentColor;
    }
    private void DebouncedThreatUI(float t, float intensityWhenOn)
    {
        if (threatVisualOn)
        {
            if (t < threatOffThreshold)
            {
                threatVisualOn = false;
                threatRearmUntil = Time.time + threatRearmDelay; 
                UpdateThreatOverlay(0f); 
            }
            else
            {
                UpdateThreatOverlay(intensityWhenOn);
            }
            return;
        }

        if (Time.time >= threatRearmUntil && t >= threatOnThreshold)
        {
            threatVisualOn = true;
            UpdateThreatOverlay(intensityWhenOn);
        }
        else
        {
            UpdateThreatOverlay(0f);
        }
    }

    void FixedUpdate()
    {
        PlayerMovement();
        CameraFollow();
    }
    private void OnEnable()
    {
        AssignEyeButton();
    }

    private void OnDisable()
    {
        UnassignEyeButton();
    }

    private void AssignEyeButton()
    {
        if (eyeButton == null)
        {
            return;
        }

        eyeButton.GetComponent<Button>().onClick.RemoveAllListeners();

        eyeButton.GetComponent<Button>().onClick.AddListener(OnEyeButtonClick);

    }

    private void UnassignEyeButton()
    {
        if (eyeButton == null) return;

        eyeButton.GetComponent<Button>().onClick.RemoveListener(OnEyeButtonClick);
    }
    public void TryOpenDoor()
    {
        if (!playerInTheDoorOpenZone) return;

        if (playerFoundTheKey)
        {
            OpenDoorSuccess();
        }
        else
        {
            OpenDoorFail();
        }
    }

    private void OpenDoorSuccess()
    {
        GameManager.audioSourca.PlayOneShot(GameManager.DoorOpening);
        var door = GameObject.FindGameObjectWithTag("Door");
        door?.GetComponent<Animator>().Play("DoorOpening");

        if (doorCollider != null) doorCollider.enabled = false;
        if (doorTrigger != null) doorTrigger.enabled = false;

        moveActionToUse.action.Disable();
        GameManager.GameIsFinishedSuccess(); 
    }

    private void OpenDoorFail()
    {
        GameManager.audioSourca.PlayOneShot(GameManager.DoorForce);
        youHaveToFindTheKeyWarning.SetActive(true);
        StartCoroutine(HideWarning(youHaveToFindTheKeyWarning));
    }

    private void PlayerMovement()
    {
        if (!GameManager.GameIsActive) return;

        Vector2 move = moveActionToUse.action.ReadValue<Vector2>();
        float currentSpeed = GameManager.GameIsActive ? moveSpeed : 0f;

        animator.Play(move.magnitude > 0.1f ? "Run" : "Idle");
        if (move.x < 0) GetComponent<SpriteRenderer>().flipX = true;
        if (move.x > 0) GetComponent<SpriteRenderer>().flipX = false;

        transform.Translate(move * currentSpeed * Time.deltaTime);
    }

    private void CameraFollow()
    {
        if (!GameManager.GameIsActive) return;

        Vector3 targetPos;
        float targetSize;

        if (GameManager.eyeIsActive)
        {
            targetPos = new Vector3(0f, 0f, -10f); 
            targetSize = 5f + (GameManager.Level * 0.52f);
            targetSize = Mathf.Clamp(targetSize, 6f, 29f);
        }
        else
        {
            targetPos = new Vector3(
                transform.position.x,
                transform.position.y,
                -10f
            );

            float wideSize = 5f + (GameManager.Level * 0.1f);
            targetSize = wideSize * 0.66f;
            targetSize = Mathf.Clamp(targetSize, 4.2f, 11f);
        }

        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            targetPos,
            ref velocity,
            damping
        );

        mainCamera.orthographicSize = Mathf.Lerp(
            mainCamera.orthographicSize,
            targetSize,
            Time.deltaTime * 2f
        );
    }
    private IEnumerator HideWarning(GameObject warning)
    {
        yield return new WaitForSeconds(2f);
        warning.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") && !isInCloset && !isHidden)
        {
            GameManager.GameIsFinishedFailure();
        }

        if (!other.TryGetComponent<ObjectIdentity>(out var identity))
        {
            HandleSpecialTag(other);
            return;
        }

        if (other.CompareTag("GameObjects"))
        {
            ProcessGameObjects(other, identity);
        }
    }

    private void HandleSpecialTag(Collider2D other)
    {
        switch (other.tag)
        {
            case "Door":
                playerInTheDoorOpenZone = true;
                pressEWarning.SetActive(true);
                StartCoroutine(HideWarning(pressEWarning));
                break;

            case "Chest":
                CollectChest(other);
                break;

            case "Treasure":
                CollectTreasure(other);
                break;
        }
    }

    private void ProcessGameObjects(Collider2D other, ObjectIdentity identity)
    {
        other.GetComponent<Collider2D>().enabled = false;
        var sr = other.GetComponent<SpriteRenderer>();
        var anim = other.GetComponent<Animator>();

        if (sr != null) sr.enabled = true;

        switch (identity.objectType)
        {
            case ObjectType.TimeSlow:
                ApplyTimeSlow(other, sr);
                break;
            case ObjectType.Bomb:
                ApplyBomb(other, anim);
                break;
            case ObjectType.Heal:
                ApplyHeal(other);
                break;
            case ObjectType.Eye:
                ActivateEye(other, anim, sr);
                break;
        }
    }

    private void CollectChest(Collider2D chestCollider)
    {
        playerFoundTheKey = true;
        chestCollider.enabled = false;
        moveActionToUse.action.Disable();
        moveSpeed = 0;
        GameManager.GameIsActive = false;
        GameManager.audioSourca.PlayOneShot(GameManager.ChestOpenAudioClip);
        chest.GetComponent<Animator>().Play("chestOpen");
        StartCoroutine(ChestSequence(chestCollider.gameObject));
    }

   
    private void ApplyTimeSlow(Collider2D obj, SpriteRenderer sr)
    {
        GameManager.GameObjects.Remove(obj.gameObject);
        GameManager.audioSourca.PlayOneShot(GameManager.SlowEffect);
        moveSpeed = 0.5f;
        StartCoroutine(ResetSpeed(3f));
        Destroy(obj.gameObject, 1.5f);
    }

    private void ApplyBomb(Collider2D obj, Animator anim)
    {
        GameManager.GameObjects.Remove(obj.gameObject);
        GameManager.audioSourca.PlayOneShot(GameManager.BombAudioClip);
        anim?.Play("bombExplosion");
        playerHeal -= 50;
        Destroy(obj.gameObject, 1.2f);
    }

    private void ApplyHeal(Collider2D obj)
    {
        GameManager.GameObjects.Remove(obj.gameObject);
        GameManager.audioSourca.PlayOneShot(GameManager.HealAudioClip);
        playerHeal = Mathf.Min(playerHeal + 25, 100);
        StartCoroutine(ShowHealEffect());
        Destroy(obj.gameObject, 1.3f);
    }

    private void ActivateEye(Collider2D obj, Animator anim, SpriteRenderer sr)
    {
        GameManager.GameObjects.Remove(obj.gameObject);
        GameManager.audioSourca.PlayOneShot(GameManager.EyeAudioClip);
        GameManager.eyeIsActive = true;
        anim?.Play("eyeMove");
        moveSpeed = 0;
        StartCoroutine(EyeVisibleCountdown(3, obj.gameObject));
    }

    private IEnumerator ResetSpeed(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        moveSpeed = 3f;
    }

    private IEnumerator ShowHealEffect()
    {
        healEffect.SetActive(true);
        yield return new WaitForSeconds(3f);
        healEffect.SetActive(false);
    }

    private IEnumerator ChestSequence(GameObject chestObj)
    {
        yield return new WaitForSeconds(1.5f);
        chestObj.GetComponent<Animator>().Play("chestStayOpen");
        key.GetComponent<Animator>().Play("KeyRiseUp");
        yield return new WaitForSeconds(1.5f);
        key.SetActive(false);
        moveActionToUse.action.Enable();
        moveSpeed = 3f;
        GameManager.GameIsActive = true;
    }

    private IEnumerator EyeVisibleCountdown(int duration, GameObject eye)
    {
        moveSpeed = 0;

        if (key != null) key.GetComponent<SpriteRenderer>().enabled = false;

        tempRenderers.Clear();
        foreach (var obj in GameManager.GameObjects)
        {
            if (obj == null || obj == chest || obj == key) continue;
            if (obj.TryGetComponent<SpriteRenderer>(out var sr))
            {
                sr.enabled = true;
                tempRenderers.Add(sr);
            }
        }

        yield return new WaitForSeconds(duration * 2);

        foreach (var sr in tempRenderers)
        {
            if (sr != null) sr.enabled = false;
        }

        eyeIsActive = false;
        GameManager.eyeIsActive = false;

        yield return new WaitForSeconds(0.1f);
        if (chest != null) chest.GetComponent<SpriteRenderer>().enabled = true;
        if (key != null) key.GetComponent<SpriteRenderer>().enabled = true;

        moveSpeed = 3f;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Door"))
            playerInTheDoorOpenZone = false;
    }
}