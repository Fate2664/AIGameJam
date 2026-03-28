using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeathScreen : MonoBehaviour
{
    public GameObject deathScreen;
    public Button restartButton;
    public Button menuButton;

    [SerializeField] private Volume _volume;
    [Range(-100, 100)]
    [SerializeField] private float _saturation = 0f;
    [Range(-10, 10)]
    [SerializeField] private float _postExposure = 0f;
    private PauseVolume pauseVolume;
    void Start()
    {
        deathScreen.SetActive(false);   //Don't show the death screen when game begins
        restartButton.onClick.AddListener(RestartGame);     //When clicked on the restart button call the RestartGame method
        menuButton.onClick.AddListener(LoadMainMenu);         //When clicked on the menu button call the RestartGame method
        pauseVolume = new PauseVolume(_volume, _saturation, _postExposure);
    }

    public void ShowDeathScreen()
    {

        deathScreen.SetActive(true);      //Show the death screen
        pauseVolume.ApplyPauseEffect();
        //Time.timeScale = 0f;            //Freeze the game

    }

    private void RestartGame()
    {
        Time.timeScale = 1f;            //reset the game timer
        pauseVolume.RemovePauseEffect();
        SceneManager.LoadScene("Level 1");      //reload the level

    }

    private void LoadMainMenu()
    {
        Time.timeScale = 1f;            //reset the game timer
        SceneManager.LoadScene("Menu");      //load the main menu
    }

}
