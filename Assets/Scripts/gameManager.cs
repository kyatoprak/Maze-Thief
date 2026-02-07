using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class gameManager : MonoBehaviour
{
    private int mazeSize;
    [Header("Game Objects")]
    public GameObject playerPrefab;
    public List<GameObject> objectPrefabs; // Eye, Bomb, Heal, Slow
    public GameObject chest;
    public GameObject fantasmaPrefab; // Ghost
    public GameObject closetPrefab;
    public GameObject treasurePrefab;
    public NavMeshSurface navMeshSurface;
    public Button closetActionButton; 
    public ClosetScript currentActiveCloset; 
    [HideInInspector] public List<ClosetScript> allSpawnedClosets = new List<ClosetScript>();

    [Header("Audio Clips / Sources")]
    public AudioSource audioSourca;
    public AudioSource musicAudioSource;
    public AudioClip HealAudioClip;
    public AudioClip TreasureAudioClip;
    public AudioClip BombAudioClip;
    public AudioClip EyeAudioClip;
    public AudioClip SlowEffect;
    public AudioClip GameStartAudioClip;
    public AudioClip ChestOpenAudioClip;
    public AudioClip DoorForce;
    public AudioClip DoorOpening;

    [Header("Game Over Panels")]
    public GameObject successPanel;
    public GameObject failurePanel;

    [Header("UI Objects")]
    public TMP_Text resumeGameTimer;
    public GameObject GameIsFinishedPanel;
    public GameObject pauseMenu;
    public Image BlackOutPanel;
    public TMP_Text TreasureCounterText;
    public TMP_Text LevelInfoText;

    [Header("Options Menu")]
    public GameObject optionsPanel;
    public Slider musicSlider;
    public Slider sfxSlider;

    private float _prevTimeScale = 1f;

    private Camera camera;
    private MazeGenerator _mazeGenerator;
    private playerScript _playerScript;

    [HideInInspector] public List<GameObject> treasures = new List<GameObject>();
    [HideInInspector] public List<AStarAI> ghosts = new List<AStarAI>();
    public List<GameObject> GameObjects = new List<GameObject>();

    public bool GameIsActive = false;
    public bool eyeIsActive = false;
    public int Level = 1;

    public int totalTreasuresInLevel;
    private int collectedTreasures = 0;
    
    float alpha = 0f;
    float velocity = 0f;
    public bool blackOutBool = false;

    // ================== LIFECYCLE ==================
    void Awake()
    {
        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);
        }

        float music = PlayerPrefs.GetFloat("opt_music", 0.8f);
        float sfx = PlayerPrefs.GetFloat("opt_sfx", 0.8f);

        if (musicSlider != null) musicSlider.value = music;
        if (sfxSlider != null) sfxSlider.value = sfx;

        ApplyMusic(music);
        ApplySFX(sfx);

        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    void Start()
    {
        _mazeGenerator = GetComponent<MazeGenerator>();
        StartCoroutine(WaitForMazeScript());
        camera = Camera.main;
        Time.timeScale = 1;
        
            UpdateLevelInfo();

    }
    private void UpdateLevelInfo()
    {
        if (LevelInfoText == null) return;

        string difficulty = Level switch
        {
            <= 10  => "Easy",
            <= 25 => "Medium",
            _     => "Hard"
        };
        Color difficultyColor = Level switch
        {
            <= 10  => Color.green,
            <= 25 => new Color(1f, 0.6f, 0f),
            _     => Color.red
        };

        LevelInfoText.color = difficultyColor;
        LevelInfoText.text = $"Level {Level}";
    }
    public void OnClosetActionButtonClicked()
    {

        if (currentActiveCloset != null)
        {
            currentActiveCloset.ToggleCloset();
        }
        
    }
    IEnumerator WaitForMazeScript()
    {
        yield return new WaitUntil(() => _mazeGenerator != null);

        Level = PlayerPrefs.GetInt("Level", 1);
        Level = Mathf.Clamp(Level, 1, 50); 

         mazeSize = 9 + Level;
        _mazeGenerator.width = mazeSize;
        _mazeGenerator.height = mazeSize;

        camera.orthographicSize = 5f + (Level * 0.52f);

        _mazeGenerator.GenerateMaze();
        StartCoroutine(WaitForMazeAndSpawn());
    }

    IEnumerator WaitForMazeAndSpawn()
    {
        yield return new WaitUntil(() => MazeGenerator.Instance != null && MazeGenerator.Instance.pathMazes.Count > 0);
        SpawnEveryObject();
    }

    IEnumerator BlackOutScreen(int sec)
    {
        yield return new WaitForSeconds(sec);
        _playerScript = GameObject.FindGameObjectWithTag("Player").GetComponent<playerScript>();
        yield return StartCoroutine(BlackOutToBlack());
        yield return new WaitForSeconds(2f);
        yield return StartCoroutine(BlackOutToWhite());
    }
    
    public void LoadNextLevel()
    {
        Level++;

        if (Level > 50)
        {
            SceneManager.LoadScene("MainMenuScene");
            return;
        }

        PlayerPrefs.SetInt("Level", Level);
        PlayerPrefs.Save();

        SceneManager.LoadScene(1); 
    }

    private Vector3 GetPositionAwayFromPlayer(List<Vector3> candidates, List<Vector3> used, Vector3 playerPos, float minDistance, System.Random rng)
    {
        List<Vector3> available = new List<Vector3>(candidates);
        available.RemoveAll(pos => used.Contains(pos));

        List<Vector3> validPositions = new List<Vector3>();
        foreach (Vector3 pos in available)
        {
            if (Vector3.Distance(pos, playerPos) >= minDistance)
            {
                validPositions.Add(pos);
            }
        }

        if (validPositions.Count == 0)
        {
            for (float dist = minDistance - 2f; dist >= 4f; dist -= 1f)
            {
                foreach (Vector3 pos in available)
                {
                    if (Vector3.Distance(pos, playerPos) >= dist)
                    {
                        validPositions.Add(pos);
                    }
                }
                if (validPositions.Count > 0) break;
            }
        }

        if (validPositions.Count == 0)
        {
            return GetUniqueRandomPosition(candidates, used, rng);
        }

        return validPositions[rng.Next(validPositions.Count)];
    }
    
    // ================== SPAWN LOGIC ==================
    private void SpawnEveryObject()
    {
        List<Vector3> usedPositions = new List<Vector3>();
        List<Vector3> pathTiles = new List<Vector3>(MazeGenerator.Instance.pathMazes);
        System.Random rng = new System.Random();

        
        GameObject door = GameObject.FindGameObjectWithTag("Door");
        if (door != null)
        {
            usedPositions.Add(door.transform.position);
        }
        
        Vector3 doorPos = door != null ? door.transform.position : Vector3.zero;

        Vector3 chestPos = GetPositionAwayFromPoint(pathTiles, usedPositions, doorPos, minDistance: mazeSize * 0.6f, rng);
        GameObject chestGO = Instantiate(chest, chestPos, Quaternion.identity);
        var chestSR = chestGO.GetComponent<SpriteRenderer>();
        if (chestSR != null) chestSR.enabled = true;
        usedPositions.Add(chestPos);

        Vector3 playerPos = GetPositionAwayFromPoint(pathTiles, usedPositions, chestPos, minDistance: mazeSize * 0.5f, rng);
        GameObject playerGO = Instantiate(playerPrefab, playerPos, Quaternion.identity);
        usedPositions.Add(playerPos);

        int ghostCount = Level switch
        {
            <= 10 => 0,
            <= 25 => 1,
            _ => 2
        };

        for (int i = 0; i < ghostCount; i++)
        {
            Vector3 ghostPos = GetPositionAwayFromPlayer(pathTiles, usedPositions, playerPos, minDistance: 8f, rng);
            Instantiate(fantasmaPrefab, ghostPos, Quaternion.identity);
            usedPositions.Add(ghostPos);
        }

        foreach (GameObject obj in objectPrefabs)
        {
            if (obj == null) continue;

            int spawnCount = Level switch
            {
                <= 10 => 0,
                <= 25 => 2, 
                _ => 3    
            };

            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 pos = GetUniqueRandomPosition(pathTiles, usedPositions, rng);
                GameObject instance = Instantiate(obj, pos, Quaternion.identity);
                var sr = instance.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = true;
                GameObjects.Add(instance);
                usedPositions.Add(pos);
            }
        }

        if (treasurePrefab != null)
        {
            totalTreasuresInLevel = Level switch
            {
                <= 10  => 1,
                <= 25 => 3,
                _     => 5
            };

            for (int i = 0; i < totalTreasuresInLevel; i++)
            {
                Vector3 pos = GetUniqueRandomPosition(pathTiles, usedPositions, rng);
                if (pos == Vector3.zero) continue;

                GameObject treasureGO = Instantiate(treasurePrefab, pos, Quaternion.identity);
                var sr = treasureGO.GetComponent<SpriteRenderer>();
                if (sr != null) sr.enabled = true;

                treasures.Add(treasureGO);
                usedPositions.Add(pos);
            }
        }

        if (Level >= 11 && closetPrefab != null)
        {
            int closetCount = Level >= 11 ? Mathf.FloorToInt(Level / 4f) : 0;
            SpawnClosets(pathTiles, usedPositions, rng, closetCount);
        }

        _playerScript = playerGO.GetComponent<playerScript>();
        
        UpdateTreasureCounter(); 
        UpdateLevelInfo();
        StartCoroutine(BlackOutScreen(3));
    }
    private Vector3 GetPositionAwayFromPoint(List<Vector3> candidates, List<Vector3> used, Vector3 targetPos, float minDistance, System.Random rng)
    {
        List<Vector3> available = new List<Vector3>(candidates);
        available.RemoveAll(pos => used.Contains(pos));

        List<Vector3> validPositions = new List<Vector3>();
        foreach (Vector3 pos in available)
        {
            if (Vector3.Distance(pos, targetPos) >= minDistance)
            {
                validPositions.Add(pos);
            }
        }

        if (validPositions.Count == 0)
        {
            for (float dist = minDistance - 2f; dist >= 4f; dist -= 1f)
            {
                foreach (Vector3 pos in available)
                {
                    if (Vector3.Distance(pos, targetPos) >= dist)
                    {
                        validPositions.Add(pos);
                    }
                }
                if (validPositions.Count > 0) break;
            }
        }

        if (validPositions.Count == 0)
        {
            return GetUniqueRandomPosition(candidates, used, rng);
        }

        return validPositions[rng.Next(validPositions.Count)];
    }
    

    public void SetActiveCloset(ClosetScript closet)
    {
        currentActiveCloset = closet;
        if (closetActionButton != null)
        {
            closetActionButton.gameObject.SetActive(true);
        }
    }

    public void ClearActiveCloset(ClosetScript closet)
    {
        if (currentActiveCloset == closet)
        {
            currentActiveCloset = null;
            if (closetActionButton != null)
            {
                closetActionButton.gameObject.SetActive(false);
            }
        }
    }
    
   

    public void RegisterCloset(ClosetScript closet)
    {
        if (!allSpawnedClosets.Contains(closet))
        {
            allSpawnedClosets.Add(closet);
        }
    }
    public void UpdateTreasureCounter(int amount = 0)
    {
        if (amount > 0)
        {
            collectedTreasures += amount;
            if (audioSourca != null && TreasureAudioClip != null)
                audioSourca.PlayOneShot(TreasureAudioClip);
        }

        collectedTreasures = Mathf.Clamp(collectedTreasures, 0, totalTreasuresInLevel);

        if (TreasureCounterText != null)
        {
            TreasureCounterText.text = $"Treasure Count: {collectedTreasures}/{totalTreasuresInLevel}";
        }

        
    }
    
    
    // ================== CLOSET SPAWN ==================
  private void SpawnClosets(List<Vector3> allPathPositions, List<Vector3> usedPositions, System.Random rng, int totalClosets)
  {
      if (closetPrefab == null || totalClosets == 0) return;
  
      List<Vector3> candidatePositions = new List<Vector3>();
  
      // 1. Aday pozisyonları topla: sadece dead-end ve önemli yerlere uzak olanlar
      foreach (Vector3 pos in allPathPositions)
      {
          if (IsTooCloseToImportant(pos, usedPositions)) continue;
          if (IsDeadEnd(pos)) candidatePositions.Add(pos);
      }
  
      // Eğer yeterli aday yoksa → normal yollar da ekle ama dead-end öncelikli
      if (candidatePositions.Count < totalClosets)
      {
          foreach (Vector3 pos in allPathPositions)
          {
              if (!IsTooCloseToImportant(pos, usedPositions) && !candidatePositions.Contains(pos))
              {
                  candidatePositions.Add(pos);
              }
          }
      }
  
      List<Vector3> spawnedClosetPositions = new List<Vector3>();
  
      for (int i = 0; i < totalClosets; i++)
      {
          if (candidatePositions.Count == 0) break;
  
          // En uzak pozisyonu seç (diğer dolaplardan)
          Vector3 bestPos = GetFurthestPosition(candidatePositions, spawnedClosetPositions, rng);
  
          if (usedPositions.Contains(bestPos)) continue;
  
          GameObject closetGO = Instantiate(closetPrefab, bestPos, Quaternion.identity);
          SpriteRenderer sr = closetGO.GetComponent<SpriteRenderer>();
          if (sr != null) sr.enabled = true;
  
          usedPositions.Add(bestPos);
          spawnedClosetPositions.Add(bestPos);
          candidatePositions.Remove(bestPos);
  
          ClosetScript closetScript = closetGO.GetComponent<ClosetScript>();
          if (closetScript != null)
          {
              RegisterCloset(closetScript);
          }
      }
  }
    private bool IsTooCloseToImportant(Vector3 pos, List<Vector3> importantPositions)
    {
        return importantPositions.Exists(imp => Vector3.Distance(pos, imp) < 3f);
    }

    private bool IsDeadEnd(Vector3 worldPos)
    {
        Vector2Int gridPos = WorldToGrid(worldPos);
        int[,] maze = MazeGenerator.Instance.Maze;
        int w = maze.GetLength(0), h = maze.GetLength(1);
        int[] dx = { 0, 0, 1, -1 }, dy = { 1, -1, 0, 0 };
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            int nx = gridPos.x + dx[i]; int ny = gridPos.y + dy[i];
            if (nx >= 0 && ny >= 0 && nx < w && ny < h && maze[nx, ny] == 0) count++;
        }
        return count == 1;
    }

    private Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float offsetX = -(MazeGenerator.Instance.width - 1) / 2f;
        float offsetY = -(MazeGenerator.Instance.height - 1) / 2f;
        return new Vector2Int(Mathf.RoundToInt(worldPos.x - offsetX), Mathf.RoundToInt(worldPos.y - offsetY));
    }

    // ================== GAME OVER ==================
    public void GameIsFinishedSuccess()
    {
        GameIsActive = false;

        int currentLevel = PlayerPrefs.GetInt("Level", 1);
        int highest = PlayerPrefs.GetInt("HighestCompletedLevel", 1);

        if (currentLevel > highest)
        {
            PlayerPrefs.SetInt("HighestCompletedLevel", currentLevel);
            PlayerPrefs.Save();
        }

        StartCoroutine(ShowSuccessAfterBlackOut());
    }

    public void GameIsFinishedFailure()
    {
        if (!GameIsActive) return;
        GameIsActive = false;
        StartCoroutine(ShowFailureAfterBlackOut());
    }

    private IEnumerator ShowFailureAfterBlackOut()
    {
        yield return StartCoroutine(BlackOutToBlack());
        yield return new WaitForSeconds(1f);
        failurePanel.SetActive(true);
    }

    private IEnumerator ShowSuccessAfterBlackOut()
    {
        yield return StartCoroutine(BlackOutToBlack());
        yield return new WaitForSeconds(0.5f);
        successPanel.SetActive(true);
    }

    // ================== PAUSE / OPTIONS ==================
    public void GameIsPaused()
    {
        if (GameIsActive)
        {
            pauseMenu.SetActive(true);
            GameIsActive = false;
            StartCoroutine(StopTimeWithDelay());
        }
    }

    private IEnumerator StopTimeWithDelay()
    {
        yield return new WaitForSeconds(0.1f);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        pauseMenu.SetActive(false);
        StartCoroutine(resumeGameTimerEnumerator());
    }

    IEnumerator resumeGameTimerEnumerator()
    {
        Time.timeScale = 1;
        resumeGameTimer.gameObject.SetActive(true);
        for (int i = 3; i > 0; i--)
        {
            resumeGameTimer.text = i.ToString();
            yield return new WaitForSeconds(1);
        }
        resumeGameTimer.text = "GO";
        yield return new WaitForSeconds(1);
        resumeGameTimer.gameObject.SetActive(false);
        GameIsActive = true;
    }

    public void GoToMainMenu() => SceneManager.LoadScene("MainMenuScene");
    public void RestartGame() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    // ================== BLACKOUT ==================
    IEnumerator BlackOutToBlack()
    {
        if (audioSourca != null && GameStartAudioClip != null)
            audioSourca.PlayOneShot(GameStartAudioClip);

        while (alpha < 0.99f)
        {
            Color c = BlackOutPanel.color;
            alpha = Mathf.SmoothDamp(alpha, 1f, ref velocity, 0.3f);
            c.a = alpha;
            BlackOutPanel.color = c;
            yield return null;
        }
    }

    IEnumerator BlackOutToWhite()
    {
        foreach (var obj in GameObjects)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }
        foreach (var obj in treasures)
        {
            var sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
        }

        while (alpha > 0.01f)
        {
            Color c = BlackOutPanel.color;
            alpha = Mathf.SmoothDamp(alpha, 0f, ref velocity, 1.3f);
            c.a = alpha;
            BlackOutPanel.color = c;
            yield return null;
        }

        GameIsActive = true;
        if (_playerScript != null)
        {
            if (_playerScript.key != null) _playerScript.key.GetComponent<SpriteRenderer>().enabled = true;
            if (_playerScript.chest != null) _playerScript.chest.GetComponent<SpriteRenderer>().enabled = true;
        }
    }

    // ================== OPTIONS ==================
    public void OnOptionsButton()
    {
        if (optionsPanel == null) return;
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        optionsPanel.SetActive(true);
    }

    public void OnOptionsCloseButton()
    {
        if (optionsPanel == null) return;
        optionsPanel.SetActive(false);
        Time.timeScale = _prevTimeScale;
    }
    private Vector3 GetFurthestPosition(List<Vector3> candidates, List<Vector3> existingClosets, System.Random rng)
    {
        if (existingClosets.Count == 0)
        {
            List<Vector3> edgeCandidates = new List<Vector3>(candidates);
            edgeCandidates.Sort((a, b) =>
            {
                float distA = Vector3.Distance(a, Vector3.zero);
                float distB = Vector3.Distance(b, Vector3.zero);
                return distB.CompareTo(distA); 
            });
            return edgeCandidates.Count > 0 ? edgeCandidates[0] : candidates[rng.Next(candidates.Count)];
        }
    
        Vector3 bestPos = candidates[0];
        float maxMinDistance = 0;
    
        foreach (Vector3 pos in candidates)
        {
            float minDistanceToAny = float.MaxValue;
            foreach (Vector3 existing in existingClosets)
            {
                float dist = Vector3.Distance(pos, existing);
                if (dist < minDistanceToAny) minDistanceToAny = dist;
            }
    
            if (minDistanceToAny > maxMinDistance)
            {
                maxMinDistance = minDistanceToAny;
                bestPos = pos;
            }
        }
    
        return bestPos;
    }

    void OnMusicSliderChanged(float v) { ApplyMusic(v); PlayerPrefs.SetFloat("opt_music", v); }
    void OnSFXSliderChanged(float v) { ApplySFX(v); PlayerPrefs.SetFloat("opt_sfx", v); }
    void ApplyMusic(float v) { if (musicAudioSource != null) musicAudioSource.volume = v; }
    void ApplySFX(float v) { if (audioSourca != null) audioSourca.volume = v; }

    
    private Vector3 GetUniqueRandomPosition(List<Vector3> candidates, List<Vector3> used, System.Random rng)
    {
        for (int i = 0; i < 1000; i++)
        {
            Vector3 candidate = candidates[rng.Next(candidates.Count)];
            if (!used.Contains(candidate)) return candidate;
        }
        return candidates[0]; 
    }

    
    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }
    
    public void NotifyPlayerInCloset(bool isInCloset)
    {
        foreach (var ghost in ghosts)
            ghost.PlayerIsInCloset(isInCloset);
    }
}