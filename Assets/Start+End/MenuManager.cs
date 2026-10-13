using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private OffenseData offenseData;
    public void StartGame()
    {
        if (offenseData == null)
        {
            Debug.LogError("OffenseData is missing.");
            return;
        }

        offenseData.ResetGame();
        SceneManager.LoadScene("UpgradeScene");
    }
}