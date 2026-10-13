using UnityEngine;
using TMPro;

public enum RouteCommand
{
    None,
    Up,
    Down,
    Split
}

public class ForkCommandSlot : MonoBehaviour
{
    [SerializeField] private TMP_Text commandText;

    public RouteCommand CurrentCommand { get; private set; }
        = RouteCommand.None;

    private int splitCounter = 0;

    public void SetCommand(RouteCommand command)
    {
        CurrentCommand = command;
        splitCounter = 0;

        if (commandText == null) return;

        switch (command)
        {
            case RouteCommand.Up:
                commandText.text = "UP";
                break;

            case RouteCommand.Down:
                commandText.text = "DOWN";
                break;

            case RouteCommand.Split:
                commandText.text = "SPLIT";
                break;

            default:
                commandText.text = "";
                break;
        }
    }

    public bool ShouldUseLowerPath()
    {
        if (CurrentCommand == RouteCommand.Down)
            return true;

        if (CurrentCommand == RouteCommand.Split)
        {
            bool useLower = splitCounter % 2 == 1;
            splitCounter++;
            return useLower;
        }

        return false;
    }
}