using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class MainMenuManagerScript : MonoBehaviour
{
    public Transform contentParent;
    public GameObject levelButtonPrefab;
    public GameObject tutorialPanel; 
    public Button closeButton;  
    public Button dontShowAgainButton; 
    void Start()
    {
        GenerateLevelButtons();
        ShowTutorialIfNeeded();
    }
    private void ShowTutorialIfNeeded()
    {
        bool shouldNotShowAgain = PlayerPrefs.GetInt("DontShowTutorialAgain", 0) == 1;

        if (shouldNotShowAgain)
        {
            tutorialPanel.SetActive(false);
        }
        else
        {
            tutorialPanel.SetActive(true);
        }

        SetupTutorialButtons();
    }
    
    
    private void SetupTutorialButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                tutorialPanel.SetActive(false);
            });
        }

        if (dontShowAgainButton != null)
        {
            dontShowAgainButton.onClick.RemoveAllListeners();
            dontShowAgainButton.onClick.AddListener(() =>
            {
                PlayerPrefs.SetInt("DontShowTutorialAgain", 1);
                PlayerPrefs.Save();
                tutorialPanel.SetActive(false);
            });
        }
    }
   void GenerateLevelButtons()
{
    if (levelButtonPrefab == null)
    {
        levelButtonPrefab = Resources.Load<GameObject>("LevelButtonPrefab");
        if (levelButtonPrefab == null)
        {
            return;
        }
    }

    int highestCompletedLevel = PlayerPrefs.GetInt("HighestCompletedLevel", 1);


    for (int level = 1; level <= 50; level++)
    {
        GameObject btnObj = Instantiate(levelButtonPrefab, contentParent);

        TMP_Text text = btnObj.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = level.ToString();
        }

        Button button = btnObj.GetComponent<Button>();
        Image buttonImage = btnObj.GetComponent<Image>();

        SetButtonColor(buttonImage, level);
        

        bool isUnlockedByProgress = level <= highestCompletedLevel + 1;

        bool shouldUnlock =  isUnlockedByProgress;

        if (shouldUnlock)
        {
            int levelCopy = level;
            button.onClick.AddListener(() =>
            {
                PlayerPrefs.SetInt("Level", levelCopy);
                PlayerPrefs.Save();
                SceneManager.LoadScene("testScene");
            });
        }
        else
        {
            button.interactable = false;
            buttonImage.color = new Color(0.3f, 0.3f, 0.3f, 1f); // Gri
        }
    }
}

    private void SetButtonColor(Image buttonImage, int level)
    {
        if (buttonImage == null) return;

        Color color;

        if (level <= 10)
        {
            color = new Color(0.2f, 0.8f, 0.2f); 
        }
        else if (level <= 25)
        {
            color = new Color(1.0f, 0.6f, 0.2f); 
        }
        else
        {
            color = new Color(0.8f, 0.2f, 0.2f);
        }

        buttonImage.color = color;
    }

    public void SelectLevel(int level)
    {
        PlayerPrefs.SetInt("Level", level);
        PlayerPrefs.Save();
        SceneManager.LoadScene("testScene");
    }
}