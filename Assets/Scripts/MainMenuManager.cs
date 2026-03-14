using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainMenuManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PlayGame()
    {
        // SceneManager.LoadScene("GameScene"); 
        Debug.Log("Play Game button clicked");
    }

    public void OpenTutorial()
    {
        // SceneManager.LoadScene("TutorialScene");
        Debug.Log("Tutorial opened");
    }
    public void QuitGame()
    {
        Application.Quit();
    }
}
