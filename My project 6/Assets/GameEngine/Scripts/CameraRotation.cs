using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어를 중심으로 공전하며 회전하는 카메라 스크립트
public class CameraRotation : MonoBehaviour
{
    [Header("회전 설정")]
    public Transform target; // 카메라가 바라볼 대상 (플레이어) - 사용하지 않지만 추후 활용 가능
    public float rotationSpeed = 100f; // 회전 속도 (값을 높여서 더 빠르게 수정)
    public float rotationEndThreshold = 0.1f; // 회전이 끝났다고 판단할 각도 임계값

    // 카메라의 현재 시점을 나타내는 열거형
    public enum CameraView { Front, Right, Back, Left }
    public CameraView currentView = CameraView.Front;

    public bool IsRotating { get; private set; } // 외부에서 현재 회전 중인지 확인할 수 있는 프로퍼티

    private Quaternion targetRotation; // 목표 회전값
    private Quaternion playerTargetRotation; // 플레이어의 목표 회전값
    private int viewIndex = 0; // 현재 뷰 인덱스 (0:Front, 1:Right, 2:Back, 3:Left)
    private readonly CameraView[] views = { CameraView.Front, CameraView.Right, CameraView.Back, CameraView.Left };
    
    private Quaternion initialCameraRotation; // 카메라의 초기 회전값
    private Quaternion initialPlayerRotation; // 플레이어의 초기 회전값
    private int initialViewIndex; // 초기 뷰 인덱스

    [Header("Camera Distance")]
    [Tooltip("카메라와 플레이어 사이의 거리를 조절합니다. 1f가 기본 거리입니다.")]
    public float cameraDistanceMultiplier = 1f; // 카메라 거리 배율
    private Vector3 initialOffsetDirection; // 플레이어와 카메라 사이의 초기 오프셋 방향
    private float initialOffsetMagnitude; // 플레이어와 카메라 사이의 초기 오프셋 거리
    private PlayerMovement playerMovement; // 플레이어 이동 스크립트 참조


    void Start()
    {
        if (target == null)
        {
            Debug.LogError("카메라의 Target(플레이어)이 설정되지 않았습니다!");
            enabled = false; // 스크립트 비활성화
            return;
        }

        playerMovement = target.GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogError("플레이어에서 PlayerMovement 스크립트를 찾을 수 없습니다!");
            enabled = false;
            return;
        }

        // 시작할 때의 상태를 저장합니다.
        initialCameraRotation = transform.rotation;
        initialPlayerRotation = target.rotation;
        initialViewIndex = viewIndex;
        
        // 플레이어로부터의 초기 오프셋 방향과 거리를 저장
        Vector3 initialRelativePosition = transform.position - target.position;
        initialOffsetDirection = initialRelativePosition.normalized;
        initialOffsetMagnitude = initialRelativePosition.magnitude;
        targetRotation = initialCameraRotation;
        playerTargetRotation = initialPlayerRotation;
        UpdateCurrentView();
    }

    void Update()
    {
        // [수정] 게임 오버 UI가 활성화되어 있으면 카메라 회전 입력을 무시합니다.
        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused)
        {
            return;
        }

        // Q 키를 누르면 왼쪽으로 90도 회전
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            playerTargetRotation *= Quaternion.Euler(0, 90, 0); // 플레이어의 목표 회전값 갱신
            targetRotation *= Quaternion.Euler(0, 90, 0);
            viewIndex = (viewIndex + 1) % 4; // 0, 1, 2, 3 순환
            UpdateCurrentView();
        }

        // E 키를 누르면 오른쪽으로 90도 회전
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            playerTargetRotation *= Quaternion.Euler(0, -90, 0); // 플레이어의 목표 회전값 갱신
            targetRotation *= Quaternion.Euler(0, -90, 0);
            viewIndex--;
            if (viewIndex < 0) viewIndex = 3; // 0 -> 3으로 순환
            UpdateCurrentView();
        }

    }

    // 모든 Update() 호출이 끝난 후 마지막에 호출됩니다.
    // 주로 카메라 이동 로직에 사용됩니다.
    void LateUpdate()
    {
        if (target == null) return;

        // 1. 목표 회전값을 적용하여 플레이어로부터 얼마나 떨어져야 할지 목표 위치 계산
        Vector3 currentOffset = initialOffsetDirection * (initialOffsetMagnitude * cameraDistanceMultiplier);
        Vector3 desiredPosition = target.position + (targetRotation * currentOffset);

        // 2. 현재 위치에서 목표 위치로 부드럽게 이동 (Lerp)
        transform.position = Vector3.Lerp(transform.position, desiredPosition, rotationSpeed * Time.deltaTime);

        // 3. 회전이 거의 끝났는지 확인합니다.
        if (Quaternion.Angle(transform.rotation, targetRotation) > rotationEndThreshold)
        {
            // 아직 회전 중이라면 부드럽게 회전(Slerp)을 계속합니다.
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            target.rotation = Quaternion.Slerp(target.rotation, playerTargetRotation, rotationSpeed * Time.deltaTime);
            IsRotating = true;
        }
        else
        {
            // 회전이 거의 끝났다면, 목표 각도로 즉시 이동하여 깔끔하게 마무리합니다.
            transform.rotation = targetRotation;
            target.rotation = playerTargetRotation;
            IsRotating = false;
        }
    }

    // 현재 뷰 상태를 업데이트하고 로그를 출력하는 함수
    void UpdateCurrentView()
    {
        currentView = views[viewIndex];
        Debug.Log("Current Camera View: " + currentView);
    }

    /// <summary>
    /// 카메라와 플레이어의 회전을 초기 상태로 리셋합니다.
    /// </summary>
    public void ResetCamera()
    {
        Debug.Log("카메라 위치와 회전을 초기화합니다.");

        // 내부 상태 변수들을 초기값으로 되돌립니다.
        viewIndex = initialViewIndex;
        targetRotation = initialCameraRotation;
        playerTargetRotation = initialPlayerRotation;

        // 카메라와 플레이어의 위치/회전을 즉시 초기 상태로 설정합니다. (조절된 거리 적용)
        Vector3 resetOffset = initialOffsetDirection * (initialOffsetMagnitude * cameraDistanceMultiplier);
        transform.position = target.position + (initialCameraRotation * resetOffset);
        transform.rotation = initialCameraRotation;
        target.rotation = initialPlayerRotation;
        UpdateCurrentView();
    }
}
