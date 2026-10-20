using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayAgain : MonoBehaviour
{
    [SerializeField] private OffenseData offenseData;

    public void GoToStart()
    {
        ReverseTD.Analytics.AttemptResultsScreen.Open();
    }
}
