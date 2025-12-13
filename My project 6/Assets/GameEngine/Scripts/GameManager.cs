using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리용 네임스페이스

// 게임의 전반적인 상태를 관리하는 스크립트 (싱글톤 패턴)
public class GameManager : MonoBehaviour
{
    // GameManager의 싱글톤 인스턴스
    public static GameManager Instance { get; private set; }

    [Header("UI Settings")]
    // [수정] GameObject 대신 CanvasGroup을 제어하도록 변경합니다.
    private CanvasGroup gameOverCanvasGroup;

    private bool isGamePaused = false; // 게임 일시 정지 상태
    public bool IsGamePaused => isGamePaused; // [새로운 기능] 외부에서 isGamePaused 상태를 읽을 수 있도록 프로퍼티 추가
    public bool IsGameplayActive { get; private set; } = false; // 플레이어가 움직이기 시작했는지 여부

    void Awake()
    {
        // 싱글톤 패턴 구현: 씬에 GameManager가 하나만 존재하도록 보장
        if (Instance != null && Instance != this)
        {
            // 이미 GameManager 인스턴스가 존재하면, 이 씬에 새로 생긴 GameManager는 스스로를 파괴합니다.
            Debug.Log("중복 GameManager 감지. 새로 생성된 인스턴스를 파괴합니다.");
            
            // [추가] 인스펙터 오류(SerializedObjectNotCreatableException) 방지를 위해 Animator가 있다면 비활성화합니다.
            var anim = GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            Destroy(gameObject);
            return; // 파괴 후 더 이상 진행하지 않도록 함수를 종료합니다.
        }
        else
        {
            // 최초의 GameManager인 경우, 자신을 인스턴스로 설정하고 파괴되지 않도록 합니다.
            Instance = this; // 현재 인스턴스를 싱글톤으로 설정
            DontDestroyOnLoad(gameObject); // 씬이 바뀌어도 GameManager가 파괴되지 않도록
            Debug.Log("GameManager 인스턴스 설정 및 DontDestroyOnLoad 적용 완료.");
        }

        // [수정] 씬에 있는 모든 EventSystem을 찾아서 중복을 제거합니다.
        // 이 로직은 싱글톤 인스턴스가 결정된 후에 실행되어야 가장 안정적입니다.
        var eventSystems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        if (eventSystems.Length > 1)
        {
            Debug.Log($"중복 EventSystem {eventSystems.Length - 1}개 감지. 정리합니다.");
            // 자신(GameManager)에게 속한 EventSystem을 제외한 나머지를 모두 파괴합니다.
            foreach (var es in eventSystems)
            {
                // 이 EventSystem의 부모가 현재 활성화된 GameManager 인스턴스가 아니라면
                if (es.transform.parent != Instance.transform)
                    Destroy(es.gameObject);
            }
        }
    }

    // 스크립트가 활성화될 때 호출됩니다.
    private void OnEnable()
    {
        Debug.Log("GameManager OnEnable: Subscribing to sceneLoaded event.");
        // 씬이 로드될 때마다 InitializeGame 함수를 호출하도록 이벤트에 등록합니다.
        SceneManager.sceneLoaded += InitializeGame;
    }

    // 스크립트가 비활성화될 때 호출됩니다.
    private void OnDisable()
    {
        // GameManager는 DontDestroyOnLoad이므로, 이 함수는 게임 종료 시에만 호출됩니다.
        Debug.Log("GameManager OnDisable: Unsubscribing from sceneLoaded event.");
        // 이벤트 리스너를 제거하여 메모리 누수를 방지합니다.
        SceneManager.sceneLoaded -= InitializeGame;
    }

    // 씬이 로드될 때 호출될 함수입니다.
    private void InitializeGame(Scene scene, LoadSceneMode mode) 
    { // 이 함수는 씬이 로드될 때마다 호출되어 게임 상태를 초기화합니다.
        Debug.Log($"GameManager InitializeGame called for scene: {scene.name}, mode: {mode}");
        // [수정] 씬이 로드될 때마다 "GameOverUI" 태그를 가진 UI를 다시 찾습니다.
        // 이렇게 하면 씬 재시작 시 UI 참조가 사라지는 문제를 해결할 수 있습니다.
        GameObject gameOverUIObject = GameObject.FindGameObjectWithTag("GameOverUI");
        if (gameOverUIObject != null)
        {
            gameOverCanvasGroup = gameOverUIObject.GetComponent<CanvasGroup>();
        }

        if (gameOverCanvasGroup == null)
        {
            Debug.LogError("씬에서 'GameOverUI' 태그를 가진 UI 또는 해당 UI의 CanvasGroup 컴포넌트를 찾을 수 없습니다!");
        }

        // [수정] CanvasGroup을 이용해 UI를 숨깁니다.
        SetGameOverUIVisibility(false);

        Time.timeScale = 1f; // 게임 시작 시 시간 스케일 초기화 (게임 속도 정상화)
        isGamePaused = false; // 게임 상태 초기화

        // 게임 플레이 시 마우스 커서를 숨기고 중앙에 고정
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsGameplayActive = false; // 씬이 로드될 때마다 게임 플레이 상태 초기화
        Debug.Log($"InitializeGame finished. Current Time.timeScale: {Time.timeScale}, isGamePaused: {isGamePaused}, Cursor.visible: {Cursor.visible}");
    }

    // 게임 오버를 처리하는 함수
    public void GameOver()
    {
        if (isGamePaused) return; // 이미 UI가 떠 있으면 중복 처리 방지

        Debug.Log("Player Died! Showing Game Over UI.");
        isGamePaused = true; // 게임 상태를 일시 정지로 변경

        // [수정] 플레이어가 죽으면 즉시 게임 플레이를 비활성화하여 구름을 멈춥니다.
        IsGameplayActive = false;

        SetGameOverUIVisibility(true);
        
        // 마우스 커서를 다시 보이게 하고, 움직일 수 있도록 잠금을 해제합니다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 추가적으로 플레이어 입력 비활성화, 사운드 정지 등 필요한 로직을 여기에 추가할 수 있습니다.
        // 예: FindObjectOfType<PlayerMovement>()?.enabled = false;
    }
    
    /// <summary>
    /// 플레이어가 처음 움직였을 때 호출되어 게임 플레이를 활성화합니다.
    /// </summary>
    public void StartGameplay()
    {
        if (IsGameplayActive) return; // 이미 시작되었다면 중복 실행 방지

        IsGameplayActive = true;
        Debug.Log("<color=lime>Game Start! 플레이어가 움직이기 시작했습니다.</color>");
        // 여기에 게임 시작 시 한 번만 실행될 로직을 추가할 수 있습니다 (예: 타이머 시작)
    }

    /// <summary>
    /// 게임을 완전히 멈추고 게임 오버 UI를 띄웁니다. (예: 구름에 닿았을 때)
    /// </summary>
    public void TriggerHardGameOver()
    {
        if (isGamePaused) return;

        Debug.LogWarning("HARD GAME OVER! 게임이 완전히 종료됩니다.");
        isGamePaused = true;

        SetGameOverUIVisibility(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 게임 시간을 멈춰 모든 움직임을 정지시킵니다.
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 구름에 의해 사망했을 때의 처리를 담당합니다.
    /// </summary>
    public void HandleCloudDeath()
    {
        // 플레이어 리스폰 (PlayerMovement의 HandleDeath 함수 호출)
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            player.HandleDeath("구름");
        }

        // 구름 리스폰
        RisingCloud cloud = FindFirstObjectByType<RisingCloud>();
        if (cloud != null)
        {
            cloud.ResetCloud();
        }
    }

    // [수정] 게임 재시작 버튼에 연결될 함수 (이제 UI를 닫고 게임을 재개합니다)
    public void RestartGame()
    {
        Debug.Log("Closing Game Over UI and resuming game...");
        isGamePaused = false;

        // 게임 오버 UI를 숨깁니다.
        SetGameOverUIVisibility(false);

        // 다시 마우스 커서를 숨기고 게임 플레이에 맞게 고정합니다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // [수정] 게임 플레이 상태를 초기화하여, 플레이어가 다시 움직여야 구름이 시작되도록 합니다.
        IsGameplayActive = false;
    }

    /// <summary>
    /// CanvasGroup을 제어하여 게임 오버 UI를 보여주거나 숨깁니다.
    /// </summary>
    /// <param name="isVisible">UI를 보여줄지 여부</param>
    private void SetGameOverUIVisibility(bool isVisible)
    {
        if (gameOverCanvasGroup == null) return;

        gameOverCanvasGroup.alpha = isVisible ? 1f : 0f;
        gameOverCanvasGroup.interactable = isVisible;
        gameOverCanvasGroup.blocksRaycasts = isVisible;
    }
}
