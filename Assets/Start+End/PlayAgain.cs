using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayAgain : MonoBehaviour
{
    [SerializeField] private OffenseData offenseData;

    private void Start()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
            PreviousRunSummary.Create(offenseData, canvas.GetComponentInChildren<TMPro.TMP_Text>(), true);
    }

    public void GoToStart()
    {
        if (offenseData != null)
        {
            offenseData.ResetGame();
        }

        SceneManager.LoadScene("StartGame");
    }
}