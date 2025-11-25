using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어를 중심으로 공전하며 회전하는 카메라 스크립트
public class CameraRotation : MonoBehaviour
{
    [Header("회전 설정")]
    public Transform target; // 카메라가 바라볼 대상 (플레이어) - 사용하지 않지만 추후 활용 가능
    public float rotationSpeed = 5.0f; // 회전 속도

    [Header("이동 제한")]
    public float movementLockDuration = 1.5f; // 화면 회전 후 플레이어 이동을 막을 시간

    // 카메라의 현재 시점을 나타내는 열거형
    public enum CameraView { Front, Right, Back, Left }
    public CameraView currentView = CameraView.Front;

    private Quaternion targetRotation; // 목표 회전값
    private Quaternion playerTargetRotation; // 플레이어의 목표 회전값
    private int viewIndex = 0; // 현재 뷰 인덱스 (0:Front, 1:Right, 2:Back, 3:Left)
    private readonly CameraView[] views = { CameraView.Front, CameraView.Right, CameraView.Back, CameraView.Left };
    
    private Vector3 offset; // 플레이어와 카메라 사이의 거리
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

        // 시작할 때 현재 회전값을 목표 회전값으로 초기화
        targetRotation = transform.rotation;
        playerTargetRotation = target.rotation; // 플레이어의 목표 회전값도 초기화
        // 플레이어로부터의 초기 거리와 방향을 저장
        offset = transform.position - target.position;
        UpdateCurrentView();
    }

    void Update()
    {
        // Q 키를 누르면 왼쪽으로 90도 회전
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            playerTargetRotation *= Quaternion.Euler(0, 90, 0); // 플레이어의 목표 회전값 갱신
            targetRotation *= Quaternion.Euler(0, 90, 0);
            viewIndex = (viewIndex + 1) % 4; // 0, 1, 2, 3 순환
            playerMovement.DisableMovementForDuration(movementLockDuration);
            UpdateCurrentView();
        }

        // E 키를 누르면 오른쪽으로 90도 회전
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            playerTargetRotation *= Quaternion.Euler(0, -90, 0); // 플레이어의 목표 회전값 갱신
            targetRotation *= Quaternion.Euler(0, -90, 0);
            viewIndex--;
            if (viewIndex < 0) viewIndex = 3; // 0 -> 3으로 순환
            playerMovement.DisableMovementForDuration(movementLockDuration);
            UpdateCurrentView();
        }

    }

    // 모든 Update() 호출이 끝난 후 마지막에 호출됩니다.
    // 주로 카메라 이동 로직에 사용됩니다.
    void LateUpdate()
    {
        if (target == null) return;

        // 1. 목표 회전값을 적용하여 플레이어로부터 얼마나 떨어져야 할지 목표 위치 계산
        Vector3 desiredPosition = target.position + (targetRotation * offset);

        // 2. 현재 위치에서 목표 위치로 부드럽게 이동 (Lerp)
        transform.position = Vector3.Lerp(transform.position, desiredPosition, rotationSpeed * Time.deltaTime);

        // 3. 현재 회전값에서 목표 회전값으로 부드럽게 회전 (Slerp)
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        // 4. 플레이어도 목표 회전값으로 부드럽게 회전시킵니다.
        target.rotation = Quaternion.Slerp(target.rotation, playerTargetRotation, rotationSpeed * Time.deltaTime);
    }

    // 현재 뷰 상태를 업데이트하고 로그를 출력하는 함수
    void UpdateCurrentView()
    {
        currentView = views[viewIndex];
        Debug.Log("Current Camera View: " + currentView);
    }
}
