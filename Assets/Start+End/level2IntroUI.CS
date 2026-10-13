using UnityEngine;
using UnityEngine.SceneManagement;

public class Level2IntroUI : MonoBehaviour
{
    public void ContinueToUpgrade()
    {
        SceneManager.LoadScene("UpgradeScene");
    }
}