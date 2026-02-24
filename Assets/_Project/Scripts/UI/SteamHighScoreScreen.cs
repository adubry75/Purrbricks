using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class SteamHighScoreScreen : MonoBehaviour
{
    [SerializeField] private Canvas canvas;

    private void Awake()
    {
        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>(true);

        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            canvas.enabled = false;
        }
    }


    private void Update()
    {
        if (!gameObject.activeSelf)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GameManager.Instance?.ShowMainMenu();
        }
    }

    private void OnEnable()
    {
        Debug.Log($"{name} OnEnable (instance {GetInstanceID()})");
    }

    private void OnDisable()
    {
        Debug.Log($"{name} OnDisable (instance {GetInstanceID()})");
        Debug.Log(UnityEngine.StackTraceUtility.ExtractStackTrace());
    }


    public void Show()
    {
        Debug.Log($"Show() called on {name} (instance {GetInstanceID()})");

        //if (canvas != null)
            //canvas.enabled = true;

        // If you have a root panel, enable that too

        gameObject.SetActive(true);

        Debug.Log($" AFTER: activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy}");
    }


    public void Hide()
    {
        Debug.Log("SteamHighScoreScreen: hiding leaderboard overlay.");
        gameObject.SetActive(false);
        //if (canvas != null)
        //{
            //canvas.enabled = false;
        //}
    }
}
