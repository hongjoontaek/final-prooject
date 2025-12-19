using UnityEngine;
using UnityEngine.SceneManagement; // 씬 관리용 네임스페이스
using UnityEngine.Video; // VideoPlayer를 사용하기 위해 추가
using UnityEngine.InputSystem; // Input System을 사용하기 위해 추가

// 게임의 전반적인 상태를 관리하는 스크립트 (싱글톤 패턴)
public class GameManager : MonoBehaviour
{
    // GameManager의 싱글톤 인스턴스
    public static GameManager Instance { get; private set; }

    [Header("UI Settings")]
    [SerializeField] private Key skipVideoKey = Key.Space; // 영상을 건너뛸 키
    private bool isIntroVideoPlaying = false; // 인트로 영상이 재생 중인지 여부
    // [수정] GameObject 대신 CanvasGroup을 제어하도록 변경합니다.
    private CanvasGroup gameOverCanvasGroup;
    [Header("Ending UI Settings")]
    [SerializeField] private CanvasGroup endingCanvasGroup; // 엔딩 UI CanvasGroup
    [Header("Controls UI Settings")]
    [SerializeField] private CanvasGroup controlsCanvasGroup; // 조작법 UI CanvasGroup
    [Header("Intro Video Settings")]
    [SerializeField] private VideoPlayer introVideoPlayer; // 인트로 영상 플레이어
    [SerializeField] private GameObject videoScreenObject; // 영상이 재생될 화면 오브젝트 (선택 사항)
    [Header("Audio Settings")]
    [SerializeField] private AudioSource backgroundMusicSource; // 배경 음악을 재생할 AudioSource
    [SerializeField] private AudioClip gameplayMusic; // 게임 플레이 중 재생할 배경 음악


    [Header("Camera Settings")] // 카메라 관련 설정을 위한 헤더 추가
    [SerializeField] private Camera mainCamera; // 메인 카메라 참조
    [SerializeField] private float endingCameraFOV = 60f; // 엔딩 시 카메라 FOV

    private bool isGamePaused = false; // 게임 일시 정지 상태
    public bool IsGamePaused => isGamePaused; // [새로운 기능] 외부에서 isGamePaused 상태를 읽을 수 있도록 프로퍼티 추가
    public bool IsGameplayActive { get; private set; } = false; // 플레이어가 움직이기 시작했는지 여부

    void Awake()
    {
        // 싱글톤 패턴 구현: 씬에 GameManager가 하나만 존재하도록 보장
        if (Instance != null && Instance != this)
        {
            // 이미 GameManager 인스턴스가 존재하면, 이 씬에 새로 생긴 GameManager는 스스로를 파괴합니다.
            
            
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
            

            // 메인 카메라 할당
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("씬에 'MainCamera' 태그를 가진 카메라가 없습니다. 카메라 설정을 확인해주세요.");
            }
        }

        // [수정] 씬에 있는 모든 EventSystem을 찾아서 중복을 제거합니다.
        // 이 로직은 싱글톤 인스턴스가 결정된 후에 실행되어야 가장 안정적입니다.
        var eventSystems = FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None);
        if (eventSystems.Length > 1)
        {
            
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
        
        // 씬이 로드될 때마다 InitializeGame 함수를 호출하도록 이벤트에 등록합니다.
        SceneManager.sceneLoaded += InitializeGame;
    }

    // 스크립트가 비활성화될 때 호출됩니다.
    private void OnDisable()
    {        
        // GameManager는 DontDestroyOnLoad이므로, 이 함수는 게임 종료 시에만 호출됩니다.        
        
        // 이벤트 리스너를 제거하여 메모리 누수를 방지합니다.
        SceneManager.sceneLoaded -= InitializeGame;
    }

    // 씬이 로드될 때 호출될 함수입니다.
    private void InitializeGame(Scene scene, LoadSceneMode mode) 
    { // 이 함수는 씬이 로드될 때마다 호출되어 게임 상태를 초기화합니다.
        
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

        // [추가] 씬이 로드될 때마다 "EndingUI" 태그를 가진 UI를 찾습니다.
        Debug.Log("<color=yellow>GameManager: Searching for 'EndingUI' tagged GameObject...</color>");
        GameObject endingUIObject = GameObject.FindGameObjectWithTag("EndingUI");
        if (endingUIObject != null)
        {
            endingCanvasGroup = endingUIObject.GetComponent<CanvasGroup>();
            Debug.Log($"<color=green>GameManager: Found 'EndingUI' GameObject: {endingUIObject.name}. CanvasGroup found: {endingCanvasGroup != null}</color>");
        }

        if (endingCanvasGroup == null)
        {
            Debug.LogWarning("씬에서 'EndingUI' 태그를 가진 UI 또는 해당 UI의 CanvasGroup 컴포넌트를 찾을 수 없습니다! 엔딩 UI가 표시되지 않을 수 있습니다.");
        }

        // [추가] 씬이 로드될 때마다 "ControlsUI" 태그를 가진 UI를 찾습니다.
        Debug.Log("<color=yellow>GameManager: Searching for 'ControlsUI' tagged GameObject...</color>");
        GameObject controlsUIObject = GameObject.FindGameObjectWithTag("ControlsUI");
        if (controlsUIObject != null)
        {
            controlsCanvasGroup = controlsUIObject.GetComponent<CanvasGroup>();
            Debug.Log($"<color=green>GameManager: Found 'ControlsUI' GameObject: {controlsUIObject.name}. CanvasGroup found: {controlsCanvasGroup != null}</color>");
        }

        if (controlsCanvasGroup == null)
        {
            Debug.LogWarning("씬에서 'ControlsUI' 태그를 가진 UI 또는 해당 UI의 CanvasGroup 컴포넌트를 찾을 수 없습니다! 조작법 UI가 표시되지 않을 수 있습니다.");
        }

        // [수정] CanvasGroup을 이용해 UI를 숨깁니다.
        SetGameOverUIVisibility(false);
        Debug.Log("<color=yellow>GameManager: Hiding Ending UI at start.</color>"); // 엔딩 UI 숨김 시도 로그
        SetEndingUIVisibility(false); // [추가] 엔딩 UI도 게임 시작 시 숨깁니다.
        SetControlsUIVisibility(false); // [추가] 조작법 UI도 게임 시작 시 숨깁니다.
        
        Time.timeScale = 1f; // 게임 시작 시 시간 스케일 초기화 (게임 속도 정상화)
        isGamePaused = false; // 게임 상태 초기화

        // 게임 시작 시 마우스 커서를 숨기고 중앙에 고정
        // 조작법 UI는 아직 숨겨져 있어야 합니다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        IsGameplayActive = false; // 씬이 로드될 때마다 게임 플레이 상태 초기화        

        // [추가] 인트로 영상 재생 로직
        if (introVideoPlayer != null)
        {
            Debug.Log("GameManager: Intro video player found. Preparing to play video.");
            Debug.Log($"<color=yellow>GameManager: Video Clip Resolution: {introVideoPlayer.clip.width}x{introVideoPlayer.clip.height}</color>");
            Debug.Log($"<color=yellow>GameManager: VideoPlayer state before Prepare: isPrepared={introVideoPlayer.isPrepared}, isPlaying={introVideoPlayer.isPlaying}</color>");
            // 영상 재생 준비
            introVideoPlayer.Prepare();
            introVideoPlayer.loopPointReached += OnIntroVideoFinished; // 영상 재생 완료 이벤트 등록
            introVideoPlayer.prepareCompleted += (vp) =>
            {
                // Debug.Log($"<color=cyan>GameManager: VideoPlayer Render Mode: {vp.renderMode}</color>");
                // if (vp.renderMode == VideoRenderMode.CameraFarPlane && vp.targetCamera == null)
                //     Debug.LogError("<color=red>GameManager: VideoPlayer Render Mode is CameraFarPlane, but Target Camera is NULL!</color>");
                if (vp.renderMode == VideoRenderMode.RenderTexture && vp.targetTexture == null)
                    Debug.LogError("<color=red>GameManager: VideoPlayer Render Mode is RenderTexture, but Target Texture is NULL!</color>");
                Debug.Log($"<color=cyan>GameManager: Intro video prepareCompleted event fired. isPrepared: {vp.isPrepared}, isPlaying: {vp.isPlaying}</color>");
                Debug.Log("GameManager: Intro video prepared. Playing video.");
                if (videoScreenObject != null) videoScreenObject.SetActive(true); // 영상 화면 활성화
                introVideoPlayer.Play();
                Debug.Log($"<color=cyan>GameManager: After Play() call. isPlaying: {vp.isPlaying}.</color>");
                Debug.Log($"<color=cyan>GameManager: VideoPlayer state after Play: isPrepared={introVideoPlayer.isPrepared}, isPlaying={introVideoPlayer.isPlaying}.</color>");
                isIntroVideoPlaying = true; // 영상 재생 시작 플래그 설정
                // 영상 재생 중에는 게임을 일시 정지 상태로 간주
                isGamePaused = true;
                Time.timeScale = 0f; // 영상 재생 중에는 게임 시간 정지
            };
            // introVideoPlayer.playWhileStopped = true; // 'playWhileStopped' 속성은 존재하지 않습니다. VideoPlayer의 Time Source를 Audio DSP Time으로 설정해야 합니다.
        }
        else
        {
            Debug.LogWarning("GameManager: Intro video player not assigned. Starting game directly.");
            // 영상이 없으면 바로 게임 시작
            StartGameAfterIntro();
        }
    }
    
    void Update()
    {
        // 인트로 영상이 재생 중이고, 스킵 키가 눌렸을 때
        if (isIntroVideoPlaying && Keyboard.current[skipVideoKey].wasPressedThisFrame)
        {
            Debug.Log($"<color=yellow>GameManager: Intro video skipped by pressing {skipVideoKey}.</color>");
            introVideoPlayer.Stop(); // 영상 정지
            OnIntroVideoFinished(introVideoPlayer); // 영상 완료 처리 함수 호출
        }
    }



    // [추가] 인트로 영상 재생이 완료되었을 때 호출될 함수
    private void OnIntroVideoFinished(VideoPlayer vp)
    {
        Debug.Log($"<color=magenta>GameManager: OnIntroVideoFinished event fired. VideoPlayer state: isPrepared={vp.isPrepared}, isPlaying={vp.isPlaying}.</color>");
        Debug.Log("GameManager: Intro video finished.");
        vp.loopPointReached -= OnIntroVideoFinished; // 이벤트 리스너 해제
        if (videoScreenObject != null) videoScreenObject.SetActive(false); // 영상 화면 비활성화
        isIntroVideoPlaying = false; // 영상 재생 종료 플래그 설정
        StartGameAfterIntro();
    }

    // 게임 오버를 처리하는 함수
    public void GameOver()
    {
        if (isGamePaused) return; // 이미 UI가 떠 있으면 중복 처리 방지        

        
        isGamePaused = true; // 게임 상태를 일시 정지로 변경

        // [수정] 플레이어가 죽으면 즉시 게임 플레이를 비활성화하여 구름을 멈춥니다.
        IsGameplayActive = false;

        SetGameOverUIVisibility(true);
        StopBackgroundMusic(); // 게임 오버 시 배경 음악 정지
        SetControlsUIVisibility(false); // 게임 오버 시 조작법 UI 숨김
        
        // 마우스 커서를 다시 보이게 하고, 움직일 수 있도록 잠금을 해제합니다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 추가적으로 플레이어 입력 비활성화, 사운드 정지 등 필요한 로직을 여기에 추가할 수 있습니다.
        // 예: FindObjectOfType<PlayerMovement>()?.enabled = false;
    }
    
    // [추가] 인트로 영상 재생 후 실제 게임 시작 로직
    private void StartGameAfterIntro()
    {
        isGamePaused = false; // 게임 일시 정지 해제
        Time.timeScale = 1f; // 게임 시간 다시 시작
        // 여기에 게임 시작 시 필요한 추가 로직을 넣을 수 있습니다.
        // 예: StartGameplay(); // 플레이어가 움직이기 시작할 때까지 기다리므로 이 함수는 직접 호출하지 않습니다.
    }
    /// <summary>
    /// 플레이어가 처음 움직였을 때 호출되어 게임 플레이를 활성화합니다.
    /// </summary>
    public void StartGameplay()
    {
        if (IsGameplayActive) return; // 이미 시작되었다면 중복 실행 방지

        IsGameplayActive = true;
        PlayBackgroundMusic(gameplayMusic); // 게임 플레이 시작 시 배경 음악 재생
        SetControlsUIVisibility(true); // 게임 플레이 시작 시 조작법 UI 표시
        
        // 여기에 게임 시작 시 한 번만 실행될 로직을 추가할 수 있습니다 (예: 타이머 시작)
    }

    /// <summary>
    /// 게임을 완전히 멈추고 게임 오버 UI를 띄웁니다. (예: 구름에 닿았을 때)
    /// </summary>
    public void TriggerHardGameOver()
    {
        if (isGamePaused) return;

        
        isGamePaused = true;

        SetGameOverUIVisibility(true);
        StopBackgroundMusic(); // 하드 게임 오버 시 배경 음악 정지
        SetControlsUIVisibility(false); // 하드 게임 오버 시 조작법 UI 숨김

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
        
        isGamePaused = false;

        // 게임 오버 UI를 숨깁니다.
        SetEndingUIVisibility(false); // [추가] 엔딩 UI도 숨깁니다.
        SetGameOverUIVisibility(false);
        SetControlsUIVisibility(false); // 게임 재시작 시 조작법 UI 숨김
        StopBackgroundMusic(); // 게임 재시작 시 배경 음악 정지

        // 다시 마우스 커서를 숨기고 게임 플레이에 맞게 고정합니다.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // [수정] 게임 플레이 상태를 초기화하여, 플레이어가 다시 움직여야 구름이 시작되도록 합니다.
        IsGameplayActive = false;
        // [추가] 씬에 있는 모든 부서지는 발판을 찾아서 초기화합니다.
        BreakablePlatform[] platforms = FindObjectsByType<BreakablePlatform>(FindObjectsSortMode.None);
        foreach (BreakablePlatform platform in platforms)
        {
            platform.ResetPlatform();
        }
        
        // [추가] 구름을 초기 위치로 리스폰시킵니다.
        RisingCloud cloud = FindFirstObjectByType<RisingCloud>();
        if (cloud != null)
        {
            cloud.ResetCloud();
        }

        // [추가] 기존 플레이어 이동 스크립트 활성화
        PlayerMovement originalPlayerMovement = FindFirstObjectByType<PlayerMovement>();
        if (originalPlayerMovement != null)
        {
            originalPlayerMovement.enabled = true;
            // 엔딩 모드 해제
            originalPlayerMovement.IsInEndingMode = false;
        }
        // [추가] 카메라를 초기 상태로 리셋합니다 (예: Orthographic).
        if (mainCamera != null)
        {
            mainCamera.orthographic = true; // 게임 시작 시 카메라가 Orthographic이라고 가정
        }
        // 게임 시간을 다시 1로 설정 (TriggerHardGameOver로 0이 되었을 경우 대비)
        Time.timeScale = 1f;

        
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
    
    /// <summary>
    /// [추가] CanvasGroup을 제어하여 조작법 UI를 보여주거나 숨깁니다.
    /// </summary>
    /// <param name="isVisible">UI를 보여줄지 여부</param>
    private void SetControlsUIVisibility(bool isVisible)
    {
        if (controlsCanvasGroup == null) return;
        controlsCanvasGroup.alpha = isVisible ? 1f : 0f;
        controlsCanvasGroup.interactable = isVisible;
        controlsCanvasGroup.blocksRaycasts = isVisible;
    }

    /// <summary>
    /// [추가] CanvasGroup을 제어하여 엔딩 UI를 보여주거나 숨깁니다.
    /// </summary>
    /// <param name="isVisible">UI를 보여줄지 여부</param>
    private void SetEndingUIVisibility(bool isVisible)
    {
        if (endingCanvasGroup == null) return;
        endingCanvasGroup.alpha = isVisible ? 1f : 0f;
        endingCanvasGroup.interactable = isVisible;
        endingCanvasGroup.blocksRaycasts = isVisible;
    }

    /// <summary>
    /// 카메라를 원근 투영(Perspective) 모드로 전환하고, 엔딩에 맞게 FOV와 거리를 조절합니다.
    /// </summary>
    public void SwitchCameraToPerspective() // private에서 public으로 변경
    {
        if (mainCamera == null)
        {
            Debug.LogError("메인 카메라가 할당되지 않아 카메라 모드를 변경할 수 없습니다.");
            return;
        }

        // 카메라의 투영 방식을 원근 투영으로 변경합니다.
        mainCamera.orthographic = false;
        // 원근 투영 시 사용할 Field of View(시야각)를 설정합니다.
        mainCamera.fieldOfView = endingCameraFOV;

        // CameraRotation 스크립트가 있다면, 카메라와 플레이어 사이의 거리 배율을 업데이트하여
        // 원근 투영에 맞게 카메라 위치를 자동으로 조정하도록 합니다.
        CameraRotation camRotation = mainCamera.GetComponent<CameraRotation>();
        if (camRotation != null)
        {
            camRotation.IsInEndingMode = true; // 엔딩 모드 활성화
        }
        
        
    }

    /// <summary>
    /// 게임 엔딩 시퀀스를 시작합니다. 카메라를 3D 뷰로 전환하고 게임을 마무리합니다.
    /// 이 함수는 '엔딩 블록'과 같은 특정 게임 종료 조건에서 호출되어야 합니다.
    /// </summary>
    public void StartEndingSequence()
    {
        if (isGamePaused) return; // 이미 엔딩 시퀀스 중이거나 일시 정지 상태면 중복 실행 방지

        
        // isGamePaused = true; // 엔딩 장면에서 플레이어가 움직일 수 있도록 이 줄을 주석 처리하거나 제거합니다.
        IsGameplayActive = false; // 게임 플레이 비활성화

        // 카메라를 원근 투영 모드로 전환
        SwitchCameraToPerspective();

        // 마우스 커서를 다시 보이게 하고, 움직일 수 있도록 잠금을 해제합니다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetControlsUIVisibility(false); // 엔딩 시 조작법 UI 숨김
        StopBackgroundMusic(); // 엔딩 시 배경 음악 정지

        // 구름이 있다면 멈춥니다. (RisingCloud 스크립트에 StopCloud() 메서드가 있다고 가정)
        RisingCloud cloud = FindFirstObjectByType<RisingCloud>();
        if (cloud != null)
        {
            // cloud.StopCloud(); // RisingCloud 스크립트에 이 메서드를 추가하는 것이 좋습니다.
            cloud.enabled = false; // 임시로 컴포넌트 비활성화로 대체            
            
        }

        // 플레이어 입력 비활성화
        PlayerMovement originalPlayerMovement = FindFirstObjectByType<PlayerMovement>();
        if (originalPlayerMovement != null)
        {
            // 기존 PlayerMovement 스크립트를 비활성화하지 않고, 엔딩 모드를 활성화합니다.
            originalPlayerMovement.enabled = true; // 혹시 비활성화되어 있을 경우를 대비
            originalPlayerMovement.IsInEndingMode = true; // PlayerMovement를 엔딩 모드로 전환
        }
        // 게임 시간을 멈추지 않고, 카메라 움직임이나 다른 엔딩 연출을 허용합니다.
        // 필요하다면 Time.timeScale을 조절하여 슬로우 모션 등을 연출할 수 있습니다.
        // 예: Time.timeScale = 0.5f; // 슬로우 모션 연출
        
        // 추가적으로 엔딩 크레딧 UI 등을 띄울 수 있습니다.
        SetEndingUIVisibility(true); // [추가] 엔딩 UI를 띄웁니다.

        // 엔딩 UI가 나타난 후 일정 시간 뒤에 자동으로 게임을 재시작하거나, 버튼을 통해 재시작할 수 있습니다.
        // 예: Invoke("RestartGame", 5f); // 5초 뒤 자동 재시작
    }

    /// <summary>
    /// 배경 음악을 재생합니다.
    /// </summary>
    /// <param name="musicClip">재생할 오디오 클립</param>
    private void PlayBackgroundMusic(AudioClip musicClip)
    {
        if (backgroundMusicSource != null && musicClip != null)
        {
            backgroundMusicSource.clip = musicClip;
            backgroundMusicSource.loop = true; // 배경 음악은 반복 재생
            backgroundMusicSource.Play();
        }
    }

    /// <summary>
    /// 배경 음악을 정지합니다.
    /// </summary>
    private void StopBackgroundMusic()
    {
        if (backgroundMusicSource != null && backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Stop();
        }
    }
}
