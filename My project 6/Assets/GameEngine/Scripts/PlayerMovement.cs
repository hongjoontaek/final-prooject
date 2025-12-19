using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // TextMeshPro를 사용하기 위해 이 줄을 추가합니다.

// MonoBehaviour를 상속받습니다.
public class PlayerMovement : MonoBehaviour
{

    [Header("Movement")]
    public float speed = 5f; // 플레이어 이동 속도
    public float jumpForce = 7f; // 플레이어 점프 힘    
    public Transform modelTransform; // 회전시킬 캐릭터 모델 (설정하지 않으면 Animator가 있는 오브젝트 사용)
    // public float modelRotationSpeed = 15f; // 모델 회전 속도 (이제 즉시 회전하므로 필요 없음)
    public float runRotationAngle = 135f; // [추가] 달릴 때 바라볼 각도 (90: 완전 측면, 135: 대각선/얼굴 보임)
    public Transform headTransform; // [추가] 회전을 고정할 머리 트랜스폼 (Humanoid라면 자동 할당 시도)
    public bool lockHeadRotation = true; // [추가] 머리가 항상 카메라를 바라보게 할지 여부
    public Vector3 headRotationOffset; // [추가] 머리 회전 보정값 (머리가 삐뚤어지면 이 값을 조절하세요)
    [Header("Jump Physics")]
    public float fallMultiplier = 5f; // 떨어질 때 적용할 중력 배수 (더 빨리 떨어지게 함)
    public float lowJumpMultiplier = 5f; // 짧게 점프할 때 적용할 중력 배수
    public LayerMask wallLayer; // 벽으로 인식할 레이어 (차원 접힘 방지용)

    [Header("Ground Check")]
    public LayerMask groundLayer; // 바닥으로 인식할 레이어 (Unity 에디터에서 설정)
    public float dimensionFoldDepth = 50f; // 차원 접힘을 감지할 깊이
    public float groundCheckDistance = 1.1f; // 바닥 체크를 위한 레이캐스트 거리

    [Header("Ledge Grab")]
    public Vector3 ledgeCheckOffset = new Vector3(0, 1.5f, 0); // 벽을 감지할 레이캐스트 시작 위치 (머리 근처)
    public float ledgeCheckDistance = 0.6f; // 벽을 감지할 거리
    public float ledgeTopCheckDistance = 1.2f; // 벽 위 바닥을 감지할 레이캐스트 거리
    public Vector3 climbOffset = new Vector3(0, 0.8f, 0.6f); // 올라간 후 위치 보정값
    private bool isClimbing = false; // 현재 벽을 오르는 중인지 상태를 저장할 변수

    [Header("Visibility")]
    public bool testSilhouetteMode = false; // [Debug] 체크하면 항상 실루엣 머티리얼이 보입니다.
    public bool testOverlayMode = false; // [Debug] 체크하면 항상 오버레이 머티리얼이 보입니다.
    public Material silhouetteMaterial; // 벽 뒤에 있을 때 사용할 실루엣 머티리얼
    public Material overlayMaterial; // [추가] 점프 등으로 살짝 겹쳤을 때 사용할 머티리얼 (캐릭터 모습 유지 + 벽 뚫기)
    public Transform visibilityCheckTarget; // 가려졌는지 확인할 플레이어의 부위 (보통 머리나 몸통 중앙)
    
    // [수정] 다중 렌더러 지원을 위한 구조체 및 리스트
    private class RendererData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
    }
    private System.Collections.Generic.List<RendererData> allRenderers = new System.Collections.Generic.List<RendererData>();
    
    private enum OcclusionState { None, Partial, Full } // 가려짐 상태 정의
    private OcclusionState currentOcclusionState = (OcclusionState)(-1); // [수정] 초기 상태를 유효하지 않은 값으로 설정하여 시작 시 강제 업데이트

    [Header("Fall Damage")]
    public float deathYLevel = 0f; // 이 Y축 높이 이하로 떨어지면 사망 처리
    public float maxTeleportDistance = 50f; // 자동 붙이기 기능의 최대 순간이동 거리

    [Header("Interaction")]
    public GameObject interactionPromptUI; // "Z키를 눌러 읽기" UI 오브젝트
    public TextMeshProUGUI narrationTextUI; // [수정] TextMeshPro 텍스트를 받도록 변경
    public GameObject narrationPanelUI; // 나레이션 텍스트를 담고 있는 패널
    private InteractableSign currentInteractable; // 현재 상호작용 가능한 오브젝트
    private bool isReading = false; // 현재 나레이션을 읽고 있는지 상태

    [Header("Ending")] // 새로운 헤더 추가
    public LayerMask endingBlockLayer; // 엔딩 블록 레이어

    private Rigidbody rb; // 플레이어의 Rigidbody 컴포넌트
    private CameraRotation camRotation; // 메인 카메라에 붙어있는 CameraRotation 스크립트 참조
    private bool isGrounded; // 플레이어가 바닥에 닿아있는지 여부
    private bool jumpRequested = false; // 점프 요청을 저장할 변수
    private bool dropRequested = false; // 아래로 내려가기 요청을 저장할 변수
    private bool isDropping = false; // 현재 아래로 내려가는 중인지 상태를 저장할 변수
    private Vector3 initialPosition; // 초기 스폰 위치를 저장할 변수
    public float externalHorizontalInput { get; set; } = 0f; // [추가] 외부에서 주입되는 수평 이동 입력 (-1: 왼쪽, 0: 없음, 1: 오른쪽)
    public float externalVerticalInput { get; set; } = 0f; // [추가] 외부에서 주입되는 수직 이동 입력 (-1: 뒤, 0: 없음, 1: 앞)
    public bool HasHorizontalInput { get; private set; } // [추가] 플레이어가 수평 입력을 하고 있는지 여부
    [Tooltip("캐릭터 모델의 기본 앞 방향이 월드 Z축과 다를 경우 보정 회전값 (예: 모델이 X축을 바라보면 Y=90)")]
    public Vector3 modelForwardOffset = Vector3.zero; // 모델의 앞 방향 보정 오프셋
    private Vector3 currentMoveDirection; // 현재 이동 방향 (엔딩 모드용)
    public bool IsInEndingMode { get; set; } = false; // 엔딩 모드인지 여부
    private bool hasTriggeredEnding = false; // 엔딩이 한 번 트리거되었는지 여부
    private bool wasCameraRotating = false; // 이전 프레임에서 카메라가 회전 중이었는지 확인
    private Animator animator; // [추가] 애니메이터 컴포넌트 참조    

    // 게임 시작 시 1번 호출
    void Start()
    {
        // 1. 플레이어 오브젝트에 붙어있는 Rigidbody 컴포넌트 가져오기
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody 컴포넌트를 찾을 수 없습니다! PlayerMovement GameObject에 Rigidbody를 추가해주세요.");
            enabled = false; // Rigidbody가 없으면 스크립트를 비활성화합니다.            
            return;
        }
        
        // 2. 메인 카메라에 붙어있는 CameraRotation 스크립트 가져오기
        //    이 스크립트의 currentView 값을 사용하여 플레이어 이동 방향을 결정합니다.
        camRotation = Camera.main.GetComponent<CameraRotation>();

        // 3. [수정] 플레이어의 모든 렌더러(몸통, 팔, 머리 등)를 찾아서 저장합니다.
        Renderer[] foundRenderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in foundRenderers)
        {
            // 파티클이나 트레일은 제외하고 모델만 포함시킵니다.
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;

            RendererData data = new RendererData();
            data.renderer = r;
            data.originalMaterials = r.sharedMaterials; // 원래 머티리얼 배열 저장
            allRenderers.Add(data);
        }

        if (allRenderers.Count == 0)
        {
            // Debug.LogWarning("PlayerMovement: 렌더러 컴포넌트를 찾을 수 없습니다! 플레이어가 보이지 않을 수 있습니다.");
        }
        // visibilityCheckTarget이 설정되지 않았다면, 플레이어 자기 자신을 타겟으로 설정합니다.
        if (visibilityCheckTarget == null)
            visibilityCheckTarget = transform;

        // 3. 게임 플레이 시 마우스 커서를 숨기고 중앙에 고정
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 4. [새로운 기능] 초기 위치 저장
        initialPosition = transform.position;
        
        
        // [추가] 자식 오브젝트(3D 모델)에 있는 Animator 컴포넌트를 가져옵니다.
        animator = GetComponentInChildren<Animator>();
        
        // [추가] 모델 트랜스폼이 할당되지 않았다면, 애니메이터의 트랜스폼을 기본값으로 사용합니다.
        if (modelTransform == null && animator != null)
        // [수정] 모델 트랜스폼이 할당되지 않았거나 루트와 같다면, 자식 오브젝트 중에서 적절한 모델을 찾습니다.
        if (modelTransform == null || modelTransform == transform)
        {
            modelTransform = animator.transform;
            // 1. Animator가 있는 자식 오브젝트 찾기 (루트 제외)
            if (animator != null && animator.transform != transform)
            {
                modelTransform = animator.transform;
            }
            // 2. Animator가 없다면 Renderer가 있는 자식 오브젝트 찾기
            else
            {
                Renderer childRenderer = GetComponentInChildren<Renderer>();
                if (childRenderer != null && childRenderer.transform != transform)
                {
                    modelTransform = childRenderer.transform;
                }
                // 3. 그래도 없으면 첫 번째 자식이라도 사용
                else if (transform.childCount > 0)
                {
                    modelTransform = transform.GetChild(0);
                }
            }
        }

        // [추가] 코드에서 물리 이동(Rigidbody)을 직접 제어하므로, 애니메이션에 의한 이동(Root Motion)은 비활성화합니다.
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        // [안전장치] 모델이 루트 오브젝트(Player)와 같다면 회전이 제대로 안 될 수 있음을 경고
        if (modelTransform == transform)
        {
            // Debug.LogError("치명적 오류: 'Model Transform'이 Player(루트)와 같습니다! 회전이 작동하지 않습니다. 3D 모델을 자식 오브젝트로 분리하고 Model Transform에 할당해주세요.");
            Debug.LogError("치명적 오류: 'Model Transform'이 Player(루트)와 같습니다! 회전이 작동하지 않습니다. 3D 모델을 자식 오브젝트로 분리하고 Model Transform에 할당해주세요.");
        }

        // [추가] 머리 트랜스폼이 할당되지 않았고, 캐릭터가 Humanoid라면 자동으로 머리를 찾습니다.
        if (headTransform == null && animator != null && animator.isHuman)
        {
            headTransform = animator.GetBoneTransform(HumanBodyBones.Head);            
        }
        
        if (lockHeadRotation && headTransform == null)
        {
            // Debug.LogWarning("Head Rotation Lock이 켜져 있지만, Head Transform을 찾을 수 없습니다. 인스펙터에서 할당해주세요.");
        }

        // [추가] 점프 애니메이션 속도를 물리 점프 시간에 맞춰 동기화합니다.
        SyncJumpAnimationSpeed();
    }

    // 매 프레임마다 호출 (주로 입력 감지 및 논리 처리)
    void Update()
    {
        // [수정] 게임 오버 UI가 활성화되어 있으면 모든 입력을 무시합니다.
        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused)
        {
            return;
        }

        // [새로운 기능] 상호작용 처리
        HandleInteraction();

        // [새로운 기능] 플레이어가 벽에 가려졌는지 확인하고 머티리얼을 교체합니다.
        HandleVisibility();

        // 벽 오르기 중에는 다른 모든 로직을 무시합니다.
        if (isClimbing)
        {
            return;
        }

        // 나레이션을 읽는 중에는 다른 모든 입력을 무시합니다.
        if (isReading)
        {
            return;
        }

        // 엔딩 모드일 경우, 2D 게임 로직 대신 3D 이동 로직을 사용합니다.
        if (IsInEndingMode)
        {
            HandleEndingMovementUpdate();
            return;
        }
        // 1. 바닥 체크 로직 호출 (기본 체크 및 차원 접힘 체크 포함)
        CheckGrounded(); // 새로운 바닥 체크 함수 호출
        // (확인용) Scene 뷰에 빨간색 레이저를 그려 바닥 체크가 어디서 이루어지는지 시각적으로 보여줍니다.
        Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, Color.red); 
        
        // [추가] 애니메이션 상태 업데이트 (달리기, 점프 상태 등)
        UpdateAnimations();

        // 2. 점프 (바닥에 있을 때만 점프 가능)
        bool isDownPressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;

        // 카메라가 회전 중이 아닐 때만 점프 입력을 받습니다. (점프 패드는 이 조건 무시)        
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded && !camRotation.IsRotating)
        {
            // [로그 1] 점프 입력이 들어왔을 때, 아래 키가 눌렸는지 확인합니다.
            // Debug.Log("점프 입력 감지! (아래 키 눌림: " + isDownPressed + ")");

            // 아래 방향키를 누르고 점프하면 '아래로 내려가기' 요청
            if (isDownPressed)
            {
                dropRequested = true;
                // Debug.Log("아래로 내려가기 요청 감지!");
            }            
            else // 그렇지 않으면 일반 점프 요청            
            {
                Debug.Log("일반 점프 입력 감지! (isGrounded: " + isGrounded + ")");
                jumpRequested = true; // 점프 요청을 기록
                // [추가] 점프 애니메이션 트리거 실행
                if (animator != null) animator.SetTrigger("Jump");
            }
        }

        // 3. 벽 잡기 체크 (공중에 있고, 위로 올라가는 중일 때만)
        CheckLedge();

        // 4. [새로운 기능] 추락 및 착지 감지
        HandleFallDetection();
    }

    /// <summary>
    /// 엔딩 모드일 때의 Update 로직을 처리합니다.
    /// </summary>
    private void HandleEndingMovementUpdate()
    {
        // 바닥 체크
        CheckGrounded();

        // 점프 입력 (필요하다면)
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            jumpRequested = true;
            if (animator != null) animator.SetTrigger("Jump");
        }

        // [추가] 애니메이션 상태 업데이트 (달리기, 점프 상태 등)
        UpdateAnimations();

        // [새로운 기능] 상호작용 처리
        HandleInteraction();

        // [새로운 기능] 플레이어가 벽에 가려졌는지 확인하고 머티리얼을 교체합니다.
        HandleVisibility();

        // 벽 오르기 중에는 다른 모든 로직을 무시합니다.
        if (isClimbing) return;

        // 나레이션을 읽는 중에는 다른 모든 입력을 무시합니다.
        if (isReading) return;

        // 4. [새로운 기능] 추락 및 착지 감지
        HandleFallDetection();
    }

    // [추가] 애니메이션 처리가 끝난 후 머리 회전을 덮어쓰기 위해 LateUpdate를 사용합니다.
    void LateUpdate()
    {
        // 머리 고정 기능이 켜져 있고, 머리와 카메라가 모두 존재할 때
        if (lockHeadRotation && headTransform != null && camRotation != null)
        {
            // 머리에서 카메라를 바라보는 방향 벡터 계산
            Vector3 directionToCamera = camRotation.transform.position - headTransform.position;

            // [수정] 카메라를 바라보는 회전값 생성 (Vector3.up을 사용하여 머리가 기울어지는 것 방지)
            Quaternion lookRotation = Quaternion.LookRotation(directionToCamera, Vector3.up);

            // [수정] 보정값(Offset)을 적용하여 최종 회전 적용
            // 3D 모델마다 뼈의 축이 다르므로, 인스펙터에서 headRotationOffset을 조절하여 정면을 맞추세요.
            headTransform.rotation = lookRotation * Quaternion.Euler(headRotationOffset);
        }
    }

    // [추가] 애니메이션 파라미터 업데이트 및 캐릭터 모델 회전 처리
    private void UpdateAnimations()
    {
        // 엔딩 모드일 경우, 3D 이동 애니메이션 로직을 사용합니다.
        if (IsInEndingMode)
        {
            HandleEndingAnimationUpdate();
            return;
        }
        // 1. 이동 입력 확인
        float horizontalInput = externalHorizontalInput; // 외부 수평 입력부터 시작
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontalInput -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontalInput += 1f;
        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f); // 입력값을 -1, 0, 1로 제한

        float verticalInput = externalVerticalInput; // 외부 수직 입력부터 시작
        // 만약 플레이어가 직접 W/S 키로 앞뒤 이동을 제어한다면 여기에 추가:
        // if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) verticalInput += 1f;
        // if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) verticalInput -= 1f;
        verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);

        bool isMoving = (horizontalInput != 0f || verticalInput != 0f);

        
        // 2. 애니메이터 파라미터 업데이트
        // [수정] Animator가 있을 때만 파라미터를 업데이트합니다.
        if (animator != null)
        {
            // 이동 입력이 있으면 IsRunning을 true로 설정
            // [수정] 점프 중(공중)에는 달리기 애니메이션이 재생되지 않도록 isGrounded 조건을 추가합니다.
            animator.SetBool("IsRunning", isGrounded && isMoving);
            // 바닥에 닿아있는지 여부를 IsGrounded로 설정 (점프/착지 전환용)
            animator.SetBool("IsGrounded", isGrounded);
        }

        // 3. 캐릭터 모델 회전 (이동 방향 바라보기)
        // [수정] 입력에 따라 즉시 회전하도록 단순화했습니다.
        // [수정] 모델이 루트 오브젝트(Player)와 같으면 회전시키지 않습니다. (CameraRotation과 충돌 방지)
        if (modelTransform != null && modelTransform != transform)
        {
            Quaternion targetRotation = Quaternion.Euler(0, 180, 0); // 기본값: 정면(180도)

            if (isMoving)
            {
                // 수평 입력이 우선합니다.
                if (horizontalInput > 0) // 오른쪽 입력 (D키)
            {
                // [수정] 완전 측면(90도) 대신 설정된 각도(예: 135도)를 사용하여 얼굴이 보이게 합니다.
                targetRotation = Quaternion.Euler(0, runRotationAngle, 0);
            }
            else if (horizontalInput < 0) // 왼쪽 입력 (A키)
            {
                targetRotation = Quaternion.Euler(0, -runRotationAngle, 0);
            }
                else if (verticalInput != 0f) // 수평 입력이 없고 수직 입력만 있을 경우
                {
                    // verticalInput이 1이면 카메라 기준 '앞'으로 이동 (플레이어 모델은 카메라로부터 멀어지는 방향)
                    // verticalInput이 -1이면 카메라 기준 '뒤'로 이동 (플레이어 모델은 카메라를 바라보는 방향)
                    targetRotation = Quaternion.Euler(0, verticalInput > 0 ? 180 : 0, 0);
                }
            }
            // [수정] 즉시 회전하도록 변경합니다.
            modelTransform.localRotation = targetRotation;
        }
    }

    /// <summary>
    /// 엔딩 모드일 때의 애니메이션 업데이트 로직을 처리합니다.
    /// </summary>
    private void HandleEndingAnimationUpdate()
    {
        // WASD 입력 받기
        float horizontalInput = Keyboard.current.aKey.isPressed ? -1f : (Keyboard.current.dKey.isPressed ? 1f : 0f);
        float verticalInput = Keyboard.current.sKey.isPressed ? -1f : (Keyboard.current.wKey.isPressed ? 1f : 0f);

        bool isMoving = (horizontalInput != 0f || verticalInput != 0f);

        // 애니메이터 파라미터 업데이트
        if (animator != null)
        {
            animator.SetBool("IsRunning", isGrounded && isMoving);
            animator.SetBool("IsGrounded", isGrounded);
        }

        // 캐릭터 모델 회전 (이동 방향 바라보기)
        if (modelTransform != null && modelTransform != transform)
        {
            if (isMoving)
            {
                // 카메라의 forward와 right 벡터를 XZ 평면에 투영
                Vector3 cameraForward = camRotation.transform.forward;
                cameraForward.y = 0f;
                cameraForward.Normalize();

                Vector3 cameraRight = camRotation.transform.right;
                cameraRight.y = 0f;
                cameraRight.Normalize();
                
                Vector3 moveDirection = (cameraForward * verticalInput + cameraRight * horizontalInput).normalized; // 이동 방향 계산
                // Debugging: Check the calculated move direction and target rotation
                // Debug.Log($"<color=blue>Ending Mode: Move Direction: {moveDirection}, Target Rotation Euler: {(Quaternion.LookRotation(moveDirection) * Quaternion.Euler(modelForwardOffset)).eulerAngles}</color>");

                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                modelTransform.rotation = targetRotation * Quaternion.Euler(modelForwardOffset); // 즉시 회전
            }
        }
    }


     private void CheckGrounded()
     {
         // [수정] 아래로 내려가는 중에는 바닥 체크 로직을 실행하지 않습니다.
         // 이렇게 해야 CheckGrounded가 플레이어를 다시 위로 끌어올리는 것을 막을 수 있습니다.
         if (isDropping)
         {
             isGrounded = false;
             return;
         }
         
         // 1. [수정] 기본 바닥 체크 (Raycast -> SphereCast로 변경)
         // Use combined layer mask for both modes
         LayerMask combinedGroundLayers = groundLayer | endingBlockLayer;
         Vector3 castOrigin = transform.position + Vector3.up * 0.5f;
         bool basicHit = Physics.SphereCast(castOrigin, 0.3f, Vector3.down, out RaycastHit hitInfo, groundCheckDistance, combinedGroundLayers);
         
         if (basicHit)
         {
             isGrounded = true;
             return;
         }
 
         // 2. "차원 접힘" 바닥 체크 (공중에 있을 때만 작동) - 시작 위치를 약간 위로 조정
         if (!IsInEndingMode) // 엔딩 모드에서는 차원 접힘 로직을 사용하지 않습니다.
         {
         Vector3 boxCenter = transform.position + Vector3.up * 0.5f; 
         Vector3 halfExtents; // 판의 크기 (X, Y, Z)
         float maxDistance = groundCheckDistance; // 얼마나 아래까지 체크할지
 
         // 카메라 뷰에 따라 BoxCast의 깊이 방향을 변경
         if (camRotation.currentView == CameraRotation.CameraView.Front || camRotation.currentView == CameraRotation.CameraView.Back)
         {
             // Front/Back 뷰에서는 Z축으로 깊은 상자를 사용
             halfExtents = new Vector3(0.4f, 0.5f, dimensionFoldDepth / 2);
         }
         else // Left/Right 뷰
         {
             // Left/Right 뷰에서는 X축으로 깊은 상자를 사용
             halfExtents = new Vector3(dimensionFoldDepth / 2, 0.5f, 0.4f);
         }
 
         // BoxCastAll: 보이지 않는 상자를 쏴서 충돌하는 '모든' 오브젝트를 감지합니다.
         RaycastHit[] hits = Physics.BoxCastAll(boxCenter, halfExtents, Vector3.down, Quaternion.identity, maxDistance, combinedGroundLayers);
 
         // 감지된 발판이 없다면, 공중에 있는 것으로 처리하고 함수 종료
         if (hits.Length == 0)
         {
             isGrounded = false;
             return;
         }
 
         var validHits = new System.Collections.Generic.List<RaycastHit>();
         foreach (var hit in hits)
         {
             Vector3 targetPos = transform.position;
             if (camRotation.currentView == CameraRotation.CameraView.Front || camRotation.currentView == CameraRotation.CameraView.Back)
                 targetPos.z = hit.point.z;
             else
                 targetPos.x = hit.point.x;
 
             // [새로운 기능] 너무 먼 거리는 순간이동 후보에서 제외합니다.
             float distanceToTarget = Vector3.Distance(transform.position, hit.point);
             if (distanceToTarget > maxTeleportDistance)
             {
                 continue; // 거리가 50 이상이면 이 발판은 무시하고 다음 발판을 확인합니다.
             }
 
             // [추가] 플레이어와 목표 발판 사이에 벽이 있는지 확인합니다.
             // 플레이어 위치에서 목표 위치로 레이를 쏴서 벽(Wall Layer)에 닿으면 이동하지 않습니다.
             if (Physics.Raycast(transform.position, (targetPos - transform.position).normalized, distanceToTarget, wallLayer))
             {
                 continue;
             }

             Vector3 rayStart = transform.position - (targetPos - transform.position).normalized * 0.5f;
             float distance = Vector3.Distance(rayStart, targetPos);
             Vector3 direction = (targetPos - rayStart).normalized;
 
             // [수정] 벽 검사 로직을 더 유연하게 변경합니다.
             // 1. 목표 발판이 카메라 시점에서 벽 뒤에 숨겨져 있는지 확인합니다.
             RaycastHit wallHit;
             bool isVisuallyObscured = Physics.Raycast(camRotation.transform.position, (targetPos - camRotation.transform.position).normalized, out wallHit, Vector3.Distance(camRotation.transform.position, targetPos), wallLayer);
 
             // 2. 만약 시각적으로 가려졌다면, 그 벽이 목표 발판보다 '뒤에' 있는지 확인합니다.
             //    즉, 목표 발판이 벽보다 앞에 있다면 괜찮습니다.
             if (isVisuallyObscured && wallHit.distance < Vector3.Distance(camRotation.transform.position, targetPos))
             {
                 // 목표 발판이 벽 뒤에 있으므로, 유효하지 않음.
             }
             else
             {
                 validHits.Add(hit);
             }
         }
 
         if (validHits.Count > 0)
         {
             RaycastHit closestHit = validHits[0];
             if (validHits.Count > 1)
             {
                 for (int i = 1; i < validHits.Count; i++)
                 {
                     float distToCurrentClosest = Vector3.Distance(camRotation.transform.position, closestHit.point);
                     float distToNext = Vector3.Distance(camRotation.transform.position, validHits[i].point);
 
                     if (distToNext < distToCurrentClosest)
                     {
                         closestHit = validHits[i];
                     }
                 }
             }
 
             Vector3 newPos = transform.position;
             if (camRotation.currentView == CameraRotation.CameraView.Front || camRotation.currentView == CameraRotation.CameraView.Back)
                 newPos.z = closestHit.point.z;
             else
                 newPos.x = closestHit.point.x;
             
             transform.position = newPos;
             isGrounded = true;
             return;
         }
 
         } // End of if (!IsInEndingMode)
         isGrounded = false;
     }

    // 고정된 물리 프레임마다 호출 (물리 계산에 적합)
    void FixedUpdate()
    {
        // [수정] 게임 오버 UI가 활성화되어 있으면 모든 물리 처리를 무시합니다.
        if (GameManager.Instance != null && GameManager.Instance.IsGamePaused)
        {
            return;
        }

        // 벽 오르기 중에는 물리 계산을 무시합니다.
        if (isClimbing)
        {
            return;
        }

        // 엔딩 모드일 경우, 2D 게임 로직 대신 3D 이동 로직을 사용합니다.
        if (IsInEndingMode)
        {
            HandleEndingMovementFixedUpdate();
            return;
        }
        // --- [수정된 로직] One-Way Platform 처리 ---
        // 아래로 내려가는 중이 아닐 때만, 위로 점프해서 발판을 통과할 수 있도록 합니다.
        if (!dropRequested)
        {
            // [로그 2-A] 이 로그가 계속 보인다면, dropRequested가 true로 설정되지 않았거나 이미 처리된 후입니다.            
            // Debug.Log("[FixedUpdate] 일반 충돌 처리 실행 중 (dropRequested == false)");

            // 플레이어가 위로 점프하고 있을 때(Y 속도가 양수일 때) Ground와의 충돌을 무시합니다.
            if (rb.linearVelocity.y > 0.1f)
            {
                Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Ground"), true);
            }
            else // 플레이어가 떨어지고 있거나 가만히 있을 때 충돌을 다시 활성화합니다.
            {
                Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Ground"), false);
            }
        }
        else // 아래로 내려가기 요청이 들어왔을 때
        {
            // [로그 2-B] 이 로그가 보인다면, FixedUpdate가 dropRequested를 성공적으로 감지한 것입니다.            
            // Debug.Log("<color=yellow>[FixedUpdate] 아래로 내려가기 처리 시작!</color>");
            TeleportDown(); // 순간이동 방식으로 아래로 내려갑니다.
            dropRequested = false; // 요청을 처리했으므로 초기화
        }

        // 카메라가 회전 중일 때는 모든 움직임을 멈추고 함수를 종료합니다.
        if (camRotation.IsRotating)
        {
            rb.linearVelocity = Vector3.zero; // 모든 속도를 0으로 만듭니다.
            return;
        }

        // 1. 좌/우 입력 받기 (A, D키 또는 화살표 키) - 새로운 Input System 사용
        float horizontalMoveInput = externalHorizontalInput; // 외부 수평 입력부터 시작
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontalMoveInput -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontalMoveInput += 1f;
        horizontalMoveInput = Mathf.Clamp(horizontalMoveInput, -1f, 1f); // 입력값을 -1, 0, 1로 제한

        float verticalMoveInput = externalVerticalInput; // 외부 수직 입력부터 시작
        // 만약 플레이어가 직접 W/S 키로 앞뒤 이동을 제어한다면 여기에 추가:
        // if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) verticalMoveInput += 1f;
        // if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) verticalMoveInput -= 1f;
        verticalMoveInput = Mathf.Clamp(verticalMoveInput, -1f, 1f);

        
        HasHorizontalInput = (horizontalMoveInput != 0f || verticalMoveInput != 0f); // [추가] 수평 또는 수직 입력 상태 업데이트
        // [수정] 플레이어가 처음으로 움직였을 때 GameManager에 게임 시작을 알립니다.
        if ((horizontalMoveInput != 0f || verticalMoveInput != 0f) && GameManager.Instance != null && !GameManager.Instance.IsGameplayActive)
        {
            GameManager.Instance.StartGameplay();
        }

        // 게임이 아직 시작되지 않았다면 움직이지 않습니다.
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive)
        {
            horizontalMoveInput = 0f;
            verticalMoveInput = 0f;
        }

        // 2. 현재 Rigidbody의 속도를 가져와 Y축 속도(중력, 점프)는 그대로 유지
        // Y축 속도는 그대로 두고, X와 Z축 속도만 새로 계산합니다.
        float yVelocity = rb.linearVelocity.y;

        // --- 여기가 FEZ 로직의 핵심 ---
        // 3. 카메라 뷰에 따라 '좌우' 입력을 X축 또는 Z축 속도로 변환
        //    카메라가 회전하면 플레이어의 '좌우' 개념도 카메라 시점에 맞춰 바뀝니다.

        // 카메라의 forward와 right 벡터를 XZ 평면에 투영하여 이동 방향을 계산합니다.
        Vector3 cameraForward = camRotation.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 cameraRight = camRotation.transform.right;
        cameraRight.y = 0;
        cameraRight.Normalize();

        // 최종 이동 방향 벡터 (카메라 시점 기준)
        Vector3 moveDirection = (cameraRight * horizontalMoveInput + cameraForward * verticalMoveInput);

        Vector3 velocity = moveDirection * speed;
        // Y축 속도를 다시 합쳐줍니다.
        velocity.y = yVelocity;

        // --- [새로운 로직] 지상에서 수평 이동 시 더 나은 발판으로 자동 이동 ---
        if (isGrounded && (horizontalMoveInput != 0 || verticalMoveInput != 0))
        {
            // 1. 현재 위치에서 착지 가능한 모든 발판을 찾습니다.
            Vector3 boxCenter = transform.position + Vector3.up * 0.1f;
            Vector3 halfExtents;
            if (camRotation.currentView == CameraRotation.CameraView.Front || camRotation.currentView == CameraRotation.CameraView.Back)
                halfExtents = new Vector3(0.4f, 0.5f, dimensionFoldDepth / 2);
            else
                halfExtents = new Vector3(dimensionFoldDepth / 2, 0.5f, 0.4f);

            RaycastHit[] hits = Physics.BoxCastAll(boxCenter, halfExtents, Vector3.down, Quaternion.identity, groundCheckDistance, groundLayer);

            if (hits.Length > 0)
            {
                // 2. 벽에 가려지지 않고, 현재 위치보다 카메라에 더 가까운 '최적의' 발판을 찾습니다.
                RaycastHit bestHit = new RaycastHit();
                bool foundBestHit = false;
                float closestDist = Vector3.Distance(transform.position, camRotation.transform.position);

                foreach (var hit in hits)
                {
                    // 벽 검사
                    Vector3 targetPos = transform.position;
                    if (camRotation.currentView == CameraRotation.CameraView.Front || camRotation.currentView == CameraRotation.CameraView.Back)
                        targetPos.z = hit.point.z;
                    else
                        targetPos.x = hit.point.x;

                    // [새로운 기능] 너무 먼 거리는 순간이동 후보에서 제외합니다.
                    float distanceToTarget = Vector3.Distance(transform.position, hit.point);
                    if (distanceToTarget > maxTeleportDistance)
                    {
                        continue; // 거리가 50 이상이면 이 발판은 무시하고 다음 발판을 확인합니다.
                    }

                    Vector3 rayStart = transform.position - (targetPos - transform.position).normalized * 0.5f;
                    float distance = Vector3.Distance(rayStart, targetPos);
                    Vector3 direction = (targetPos - rayStart).normalized;

                    // [수정] 벽 검사 로직을 더 유연하게 변경합니다.
                    // 1. 목표 발판이 카메라 시점에서 벽 뒤에 숨겨져 있는지 확인합니다.
                    RaycastHit wallHit;
                    bool isVisuallyObscured = Physics.Raycast(camRotation.transform.position, (targetPos - camRotation.transform.position).normalized, out wallHit, Vector3.Distance(camRotation.transform.position, targetPos), wallLayer);

                    // 2. 만약 시각적으로 가려졌다면, 그 벽이 목표 발판보다 '뒤에' 있는지 확인합니다.
                    //    즉, 목표 발판이 벽보다 앞에 있다면 괜찮습니다.
                    if (isVisuallyObscured && wallHit.distance < Vector3.Distance(camRotation.transform.position, targetPos))
                    {
                        // 목표 발판이 벽 뒤에 있으므로, 유효하지 않음.
                    }
                    else
                    {
                        // BoxCast의 충돌 지점과 카메라의 거리를 계산합니다.
                        float distToCam = Vector3.Distance(hit.point, camRotation.transform.position);

                        if (distToCam < closestDist) // 현재 위치보다 카메라에 더 가깝다면
                        {
                            closestDist = distToCam;
                            bestHit = hit;
                            foundBestHit = true;
                        }
                    }
                }

                // 3. 최적의 발판을 찾았다면, 속도가 아닌 위치를 직접 보정하여 튕김 현상을 방지합니다.
                if (foundBestHit)
                {
                    Vector3 newPos = transform.position;
                    if (camRotation.currentView == CameraRotation.CameraView.Front || camRotation.currentView == CameraRotation.CameraView.Back)
                    {
                        newPos.z = bestHit.point.z;
                    }
                    else // Left/Right 뷰
                    {
                        newPos.x = bestHit.point.x;
                    }
                    transform.position = newPos;
                }
            }
        }

        // 4. 최종 계산된 속도를 Rigidbody에 적용하여 플레이어를 이동시킵니다.
        rb.linearVelocity = velocity;

        // 5. 점프 요청이 있었으면 여기서 실제 점프를 실행합니다.
        if (jumpRequested)
        {
            ApplyJumpForce(); // 일반 점프 실행
            jumpRequested = false; // 요청을 처리했으므로 초기화
        }

        // --- [새로운 로직] 더 나은 점프 물리를 위한 처리 ---
        // 1. 떨어질 때 더 빨리 떨어지도록 중력을 강화합니다.
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        // 2. [수정] 점프 높이를 '짧게 누를 때'의 값으로 통일합니다.
        // 버튼을 길게 누르든 짧게 누르든 상관없이 항상 lowJumpMultiplier(높은 중력)를 적용하여 점프 높이를 낮게 고정합니다.
        else if (rb.linearVelocity.y > 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }
    }
    
    /// <summary>
    /// 엔딩 모드일 때의 FixedUpdate 로직을 처리합니다.
    /// </summary>
    private void HandleEndingMovementFixedUpdate()
    {
        // Disable FEZ-specific one-way platform logic
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Ground"), false); // Always allow collision

        // WASD 입력 받기
        float horizontalInput = Keyboard.current.aKey.isPressed ? -1f : (Keyboard.current.dKey.isPressed ? 1f : 0f);
        float verticalInput = Keyboard.current.sKey.isPressed ? -1f : (Keyboard.current.wKey.isPressed ? 1f : 0f);

        // 카메라의 forward와 right 벡터를 XZ 평면에 투영
        Vector3 cameraForward = camRotation.transform.forward;
        cameraForward.y = 0f;
        cameraForward.Normalize();

        Vector3 cameraRight = camRotation.transform.right;
        cameraRight.y = 0f;
        cameraRight.Normalize();

        // 이동 방향 계산
        currentMoveDirection = (cameraForward * verticalInput + cameraRight * horizontalInput).normalized;

        // Rigidbody를 이용한 이동
        if (currentMoveDirection.magnitude > 0.1f)
        {
            Vector3 targetVelocity = new Vector3(currentMoveDirection.x * speed, rb.linearVelocity.y, currentMoveDirection.z * speed);
            rb.linearVelocity = targetVelocity;
        }
        else
        {
            if (isGrounded)
            {
                rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            }
        }
        // Apply jump force if requested
        if (jumpRequested)
        {
            ApplyJumpForce();
            jumpRequested = false;
        }

        // Apply fall/low jump multipliers
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        else if (rb.linearVelocity.y > 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }

        // [수정] 플레이어가 처음으로 움직였을 때 GameManager에 게임 시작을 알립니다.
        // 엔딩 모드에서는 게임 플레이 활성화 여부와 관계없이 움직일 수 있도록 합니다.
        if ((horizontalInput != 0f || verticalInput != 0f) && GameManager.Instance != null && !GameManager.Instance.IsGameplayActive)
        {
            GameManager.Instance.StartGameplay(); // 이 부분은 엔딩 모드에서 필요 없을 수 있으나, GameManager의 다른 로직에 영향을 줄 수 있으므로 유지
        }
    }


    /// <summary>
    /// 플레이어에게 점프력을 적용합니다. 외부 스크립트(예: JumpPad)에서 호출할 수 있습니다.
    /// </summary>
    /// <param name="forceMultiplier">기본 jumpForce에 곱할 배율입니다. 1f는 일반 점프입니다.</param>
    public void ApplyJumpForce(float forceMultiplier = 1f)
    {
        if (isGrounded) // 바닥에 닿아있을 때만 점프 가능
        {
            rb.AddForce(Vector3.up * jumpForce * forceMultiplier, ForceMode.Impulse);
            
            if (animator != null) animator.SetTrigger("Jump"); // 점프 애니메이션 트리거
        }
    }

    /// <summary>
    /// [새로운 방식] 플레이어를 카메라 방향으로 1만큼 순간이동시켜 발판에서 벗어나게 합니다.
    /// </summary>
    private void TeleportDown()
    {
        // 발판 자동 붙이기 기능을 잠시 끄는 코루틴을 시작합니다.
        StartCoroutine(DisableSnappingForDuration(1f));

        // 1. 플레이어 위치에서 카메라 위치를 향하는 방향 벡터를 계산합니다.
        Vector3 directionToCamera = camRotation.transform.position - transform.position;
        // 2. Y축(높이) 값은 무시하여 수평 방향만 남깁니다.
        directionToCamera.y = 0;
        // 3. 방향 벡터를 정규화(normalize)하여 순수한 방향(길이 1)으로 만듭니다.
        Vector3 teleportDirection = directionToCamera.normalized;

        // 4. [새로운 로직] 현재 밟고 있는 발판을 찾아 그 경계까지의 거리를 계산합니다.
        float teleportDistance = 1f; // 기본 이동 거리 (발판을 못 찾을 경우 대비)
        RaycastHit groundHit;
        // 플레이어 발밑으로 레이를 쏴서 현재 서 있는 발판을 찾습니다.
        if (Physics.Raycast(transform.position, Vector3.down, out groundHit, groundCheckDistance, groundLayer))
        {
            Collider groundCollider = groundHit.collider;
            Bounds groundBounds = groundCollider.bounds;

            // 플레이어 위치에서 발판 경계까지의 거리를 계산합니다.
            // Dot product를 사용하여 특정 방향으로의 거리를 구합니다.
            float distanceToEdge = Vector3.Dot(groundBounds.extents, teleportDirection.Abs());
            float distanceOfPlayerFromCenter = Vector3.Dot(transform.position - groundBounds.center, teleportDirection);

            // 최종 이동 거리 = (경계까지 거리 - 중심에서 플레이어까지 거리) + 약간의 여유
            teleportDistance = distanceToEdge - distanceOfPlayerFromCenter + 0.1f;
        }

        // 계산된 위치로 플레이어를 순간이동시킵니다.
        transform.position += teleportDirection * teleportDistance;
        
    }

    /// <summary>
    /// 지정된 시간 동안만 발판 자동 붙이기 기능을 비활성화하는 코루틴입니다.
    /// </summary>
    /// <param name="duration">비활성화할 시간(초)</param>
    private System.Collections.IEnumerator DisableSnappingForDuration(float duration)
    {
        isDropping = true; // '내려가는 중' 상태를 활성화
        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(duration);
        // '내려가는 중' 상태를 비활성화하여 모든 자동 붙이기 기능을 다시 켭니다.
        isDropping = false;        

        // (디버그용) 기능이 다시 활성화되었음을 알림
        // Debug.Log("<color=green>발판 자동 붙이기 기능 다시 활성화.</color>");
    }

    /// <summary>
    /// [새로운 기능] 벽을 잡고 올라갈 수 있는지 확인합니다.
    /// </summary>
    private void CheckLedge()
    {
        // 땅에 있거나, 아래로 떨어지는 중이거나, 아래로 내려가기 중에는 실행하지 않음
        if (isGrounded || rb.linearVelocity.y <= 0 || isDropping)
        {
            return;
        }

        // 1. 플레이어 앞 방향 결정
        Vector3 forwardDirection = GetForwardDirection();

        // 2. 벽 감지: 플레이어 머리 근처에서 앞으로 레이캐스트
        RaycastHit wallHit;
        bool wallCheck = Physics.Raycast(transform.position + ledgeCheckOffset, forwardDirection, out wallHit, ledgeCheckDistance, groundLayer);

        if (wallCheck)
        {
            // 3. 벽 위 공간 감지: 벽 바로 위가 비어있는지 확인
            Vector3 aboveWallPoint = new Vector3(wallHit.point.x, transform.position.y + ledgeCheckOffset.y, wallHit.point.z);
            bool headClearCheck = !Physics.Raycast(aboveWallPoint, forwardDirection, ledgeCheckDistance, groundLayer);

            if (headClearCheck)
            {
                // 4. 올라갈 바닥 감지: 벽 위에서 아래로 레이캐스트
                RaycastHit ledgeHit;
                Vector3 ledgeCheckStart = wallHit.point + forwardDirection * 0.1f + Vector3.up * ledgeTopCheckDistance;
                bool ledgeCheck = Physics.Raycast(ledgeCheckStart, Vector3.down, out ledgeHit, ledgeTopCheckDistance, groundLayer);

                if (ledgeCheck)
                {
                    // 모든 조건 충족! 벽 오르기 시작
                    StartCoroutine(ClimbLedge(ledgeHit.point, forwardDirection));
                }
            }
        }
    }

    /// <summary>
    /// [새로운 기능] 실제로 벽을 오르는 동작을 처리하는 코루틴입니다.
    /// </summary>
    private System.Collections.IEnumerator ClimbLedge(Vector3 targetPosition, Vector3 forwardDirection)
    {
        isClimbing = true;
        rb.isKinematic = true; // 물리 엔진의 영향을 받지 않도록 설정
        rb.linearVelocity = Vector3.zero;

        // Debug.Log("벽 오르기 시작!");

        // 여기에 "벽 잡는 애니메이션" 트리거를 넣으면 됩니다.
        // 예: animator.SetTrigger("GrabLedge");

        // 부드럽게 벽에 붙는 과정 (선택 사항)
        Vector3 startPos = transform.position;
        Vector3 grabPos = new Vector3(targetPosition.x - forwardDirection.x * climbOffset.z, targetPosition.y - climbOffset.y, targetPosition.z - forwardDirection.z * climbOffset.z);
        float t = 0;
        while (t < 0.2f)
        {
            transform.position = Vector3.Lerp(startPos, grabPos, t / 0.2f);
            t += Time.deltaTime;
            yield return null;
        }

        // 여기에 "벽 위로 올라가는 애니메이션" 트리거를 넣으면 됩니다.
        // 예: animator.SetTrigger("ClimbUp");
        yield return new WaitForSeconds(0.8f); // 애니메이션 길이에 맞춰 대기 (임시)

        // 최종 위치로 이동
        transform.position = new Vector3(targetPosition.x, targetPosition.y, targetPosition.z);

        isClimbing = false;
        rb.isKinematic = false; // 다시 물리 엔진의 영향을 받도록 설정

        // Debug.Log("벽 오르기 완료!");
    }

    // 현재 카메라 뷰에 따른 '앞' 방향을 반환하는 헬퍼 함수
    private Vector3 GetForwardDirection()
    {
        if (camRotation.currentView == CameraRotation.CameraView.Front) return Vector3.forward;
        if (camRotation.currentView == CameraRotation.CameraView.Back) return Vector3.back;
        if (camRotation.currentView == CameraRotation.CameraView.Right) return Vector3.right;
        if (camRotation.currentView == CameraRotation.CameraView.Left) return Vector3.left;
        return transform.forward;
    }
    
    /// <summary>
    /// [새로운 기능] 플레이어가 벽에 가려졌는지 확인하고, 그에 따라 머티리얼을 교체합니다.
    /// [수정된 로직] 카메라 회전이 끝났을 때만 실루엣 모드를 켜고, 플레이어가 벽 뒤에서 나왔을 때 끕니다.
    /// </summary>
    private void HandleVisibility()
    {
        if (allRenderers.Count == 0 || silhouetteMaterial == null)
            return;

        // [Debug] 테스트 모드가 켜져 있으면 무조건 실루엣 머티리얼을 적용합니다.
        if (testSilhouetteMode)
        {
            if (currentOcclusionState != OcclusionState.Full)
            {
                SetMaterialState(silhouetteMaterial);
                currentOcclusionState = OcclusionState.Full;
            }
            return;
        }

        // [Debug] 오버레이 테스트 모드가 켜져 있으면 무조건 오버레이 머티리얼을 적용합니다.
        if (testOverlayMode)
        {
            if (currentOcclusionState != OcclusionState.Partial)
            {
                SetMaterialState(overlayMaterial);
                currentOcclusionState = OcclusionState.Partial;
            }
            return;
        }

        // [수정] 카메라 회전 중에는 깜빡임을 방지하기 위해 체크를 건너뜁니다.
        if (camRotation.IsRotating)
        {
            wasCameraRotating = true;
            return;
        }

        // [수정] 회전 중이 아닐 때는 항상 가려짐 여부를 체크합니다.
        // 이렇게 해야 플레이어가 걸어서 벽 뒤로 들어갈 때도 즉시 실루엣(또는 캐릭터)이 보입니다.
        OcclusionState newState = CheckOcclusionState();

        // [수정] 그림자 모드(Full)로의 진입은 오직 '카메라 회전 직후'에만 허용합니다.
        // 즉, 이동(좌우, 점프 등)을 통해 벽 뒤로 들어간 경우에는 그림자가 아닌 오버레이(Partial) 상태를 유지합니다.
        // 단, 이미 그림자 모드인 경우에는 그대로 유지합니다.
        if (newState == OcclusionState.Full && currentOcclusionState != OcclusionState.Full)
        {
            // 카메라 회전에 의해 가려진 것이 아니라면(즉, 이동해서 가려졌다면) 오버레이로 강제 변경
            if (!wasCameraRotating)
            {
                newState = OcclusionState.Partial;
            }
        }

        // 상태가 바뀌었을 때만 머티리얼을 교체합니다.
        if (newState != currentOcclusionState)
        {
            currentOcclusionState = newState;
            
            // Debug.Log($"Occlusion State Changed: {currentOcclusionState}");
            switch (currentOcclusionState)
            {
                case OcclusionState.Full:
                    SetMaterialState(silhouetteMaterial); // 완전 가려짐 -> 그림자
                    break;
                case OcclusionState.Partial:
                case OcclusionState.None:
                    // [수정] 평상시(None)나 부분 가려짐(Partial)일 때도 오버레이 머티리얼을 적용하여
                    // 항상 벽보다 앞에 보이도록(ZTest Always) 합니다.
                    SetMaterialState(overlayMaterial != null ? overlayMaterial : null); 
                    break;
            }
        }
        
        wasCameraRotating = false;
    }

    // [추가] 모든 렌더러의 머티리얼을 일괄 변경하는 함수
    private void SetMaterialState(Material matToApply)
    {
        foreach (var data in allRenderers)
        {
            if (data.renderer == null) continue;

            if (matToApply != null)
            {
                // 지정된 머티리얼로 교체 (실루엣 또는 오버레이)
                Material[] newMats = new Material[data.originalMaterials.Length];
                for (int i = 0; i < newMats.Length; i++)
                {
                    // [수정] 오버레이 머티리얼일 경우, 캐릭터의 원래 텍스처를 입혀서 '그대로' 보이게 합니다.
                    if (matToApply == overlayMaterial)
                    {
                        // 오버레이 머티리얼의 복사본을 만듭니다. (기본 속성 복사)
                        Material instanceMat = new Material(matToApply);
                        // 원래 머티리얼에 텍스처가 있다면 복사본에 적용합니다.
                        if (data.originalMaterials[i].HasProperty("_MainTex"))
                        {
                            instanceMat.mainTexture = data.originalMaterials[i].mainTexture;
                        }
                        newMats[i] = instanceMat;
                    }
                    else
                    {
                        // 실루엣(그림자) 등 다른 경우는 지정된 머티리얼 그대로 사용
                        newMats[i] = matToApply;
                    }
                }
                data.renderer.sharedMaterials = newMats;
            }
            else
            {
                // 원래 머티리얼로 복구
                data.renderer.sharedMaterials = data.originalMaterials;
            }
        }
    }

    // 플레이어가 벽에 가려졌는지 확인하는 헬퍼 함수
    /// <summary>
    /// 플레이어의 가려짐 상태(없음, 부분, 완전)를 확인하는 함수
    /// </summary>
    /// <returns>OcclusionState</returns>
    private OcclusionState CheckOcclusionState()
    {
        Vector3 direction = visibilityCheckTarget.position - camRotation.transform.position;
        float playerDistance = direction.magnitude;
        Vector3 cameraPos = camRotation.transform.position;
        
        // [수정] 머리, 가슴, 발 3지점을 모두 체크합니다.
        // 하나라도 보이면 '안 가려짐(Normal)'으로 판단하여, 점프 시 살짝 겹칠 때 실루엣이 되는 것을 막습니다.
        Vector3[] checkPoints = new Vector3[] 
        {
            visibilityCheckTarget.position,                 // 머리
            transform.position + Vector3.up * 0.8f,         // 가슴
            transform.position + Vector3.up * 0.2f          // 발
        };

        int blockedCount = 0;
        foreach (var point in checkPoints)
        {
            Vector3 pointDirection = point - cameraPos;
            float dist = pointDirection.magnitude;
            // [수정] 감지 거리를 늘려서 플레이어 바로 앞의 벽도 감지하도록 변경 (0.2f -> 0.01f)
            if (Physics.Raycast(cameraPos, pointDirection, dist - 0.01f, wallLayer))
            {
                blockedCount++;
            }
        }

        // (디버그용) 씬 뷰에서 초록색(안 가려짐) 또는 빨간색(가려짐) 선을 그립니다.
        // Debug.DrawRay(camRotation.transform.position, direction.normalized * (playerDistance - 0.2f), isHit ? Color.red : Color.green);

        if (blockedCount == 0) return OcclusionState.None; // 하나도 안 가려짐
        
        if (blockedCount == checkPoints.Length) return OcclusionState.Full; // 모두 가려짐 (그림자)

        return OcclusionState.Partial; // 일부만 가려짐 (캐릭터 오버레이)
    }

    private void HandleFallDetection()
    {
        if (transform.position.y < deathYLevel)
        {
            HandleDeath("추락");
        }
    }


    /// <summary>
    /// 사망 처리를 담당하는 함수입니다. (public으로 변경)
    /// </summary>
    public void HandleDeath(string cause)
    {
        // Debug.Log($"플레이어 사망! 원인: {cause}");

        // 초기 위치로 플레이어를 리스폰시킵니다.
        transform.position = initialPosition;
        // 추락하던 속도를 0으로 초기화하여 리스폰 후 바로 다시 떨어지는 것을 방지합니다.
        rb.linearVelocity = Vector3.zero;

        // GameManager를 찾아 게임 오버 UI를 띄웁니다.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }        

        // [추가] 사망 시 상호작용 관련 UI 및 상태를 초기화합니다.
        // ResetInteractionState(); // 이 함수는 이제 인라인됩니다.
        isReading = false;
        if (narrationPanelUI != null) narrationPanelUI.SetActive(false);
        if (interactionPromptUI != null) interactionPromptUI.SetActive(false);
        currentInteractable = null;
        
        // [수정] 카메라의 위치와 회전도 함께 초기화합니다.
        if (camRotation != null)
        {
            camRotation.ResetCamera();
        }
        
        // [추가] 사망 시 플레이어의 모든 렌더러를 원래 머티리얼로 복구합니다.
        SetMaterialState(null);
        currentOcclusionState = OcclusionState.None;
    }



    /// <summary>
    /// [새로운 기능] 상호작용 입력을 처리합니다.
    /// </summary>
    private void HandleInteraction()
    {
        // 나레이션을 읽고 있는 상태일 때
        if (isReading)
        {
            // Z키를 다시 누르면 나레이션을 닫습니다.
            if (Keyboard.current.zKey.wasPressedThisFrame)
            {
                isReading = false;
                narrationPanelUI.SetActive(false); // 나레이션 패널 숨기기
                // 플레이어가 여전히 상호작용 범위 안에 있다면, 다시 프롬프트를 띄웁니다.
                if (currentInteractable != null)
                {
                    interactionPromptUI.SetActive(true);
                }
            }
            return;
        }

        // 상호작용 가능한 오브젝트가 있고, Z키를 눌렀을 때
        if (currentInteractable != null && Keyboard.current.zKey.wasPressedThisFrame)
        {
            isReading = true;
            narrationTextUI.text = currentInteractable.narrationText; // UI 텍스트 설정
            narrationPanelUI.SetActive(true); // 나레이션 패널 보이기
            interactionPromptUI.SetActive(false); // 상호작용 프롬프트 숨기기
        }
    }

    // 다른 오브젝트의 트리거 콜라이더에 들어갔을 때 호출됩니다.
    private void OnTriggerEnter(Collider other)
    {
        // 트리거에 들어온 오브젝트에서 InteractableSign 스크립트를 찾아봅니다.
        InteractableSign sign = other.GetComponent<InteractableSign>();
        if (sign != null)
        {
            currentInteractable = sign; // 상호작용 대상으로 설정
            if (interactionPromptUI != null) interactionPromptUI.SetActive(true); // "Z키" 프롬프트 UI 보이기
        }
    }

    // 다른 오브젝트의 트리거 콜라이더에서 나왔을 때 호출됩니다.
    private void OnTriggerExit(Collider other)
    {
        // 트리거에서 나간 오브젝트가 현재 상호작용 대상과 같다면
        if (other.GetComponent<InteractableSign>() == currentInteractable)
        {
            // 나레이션을 읽고 있는 중이었다면 강제로 닫습니다.
            if (isReading)
            {
                isReading = false;
                narrationPanelUI.SetActive(false);
            }
            currentInteractable = null; // 상호작용 대상 초기화            
            if (interactionPromptUI != null) interactionPromptUI.SetActive(false); // "Z키" 프롬프트 UI 숨기기
        }
    }

    /// <summary>
    /// 다른 오브젝트의 일반 콜라이더와 충돌했을 때 호출됩니다.
    /// </summary>
    /// <param name="collision">충돌 정보</param>
    private void OnCollisionEnter(Collision collision)
    {
        // 1. 엔딩 블록 레이어 감지
        // (endingBlockLayer.value & (1 << collision.gameObject.layer)) != 0
        if (!hasTriggeredEnding && ((1 << collision.gameObject.layer) & endingBlockLayer) != 0)
        {
            // Debug.Log($"<color=green>Player hit Ending Block at position: {transform.position}</color>");
            // Debug.Log($"<color=green>플레이어가 엔딩 블록 레이어에 닿았습니다! 엔딩 시퀀스 시작.</color>");
            if (GameManager.Instance != null && !GameManager.Instance.IsGamePaused)
            {
                hasTriggeredEnding = true; // 엔딩이 트리거되었음을 표시
                GameManager.Instance.StartEndingSequence();
                // 엔딩이 시작되면 더 이상 다른 충돌 처리는 무시할 수 있습니다.
            }
        }
    }

    /// <summary>
    /// [새로운 기능] 물리적인 점프 체공 시간을 계산하여 애니메이션 속도를 맞춥니다.
    /// </summary>
    private void SyncJumpAnimationSpeed()
    {
        if (animator == null || rb == null) return;

        // 1. 물리적인 점프 체공 시간 계산
        float gravity = Mathf.Abs(Physics.gravity.y);
        // F = ma => v = F/m (Impulse 모드이므로 힘이 곧 운동량의 변화량)
        float jumpVelocity = jumpForce / rb.mass; 
        
        // 올라가는 시간 (v = gt => t = v/g)
        // [수정] 상승 시 항상 lowJumpMultiplier가 적용되므로 이를 반영하여 계산합니다.
        float timeToPeak = jumpVelocity / (gravity * lowJumpMultiplier);
        
        // 내려오는 시간
        // 낙하 시 중력은 gravity * fallMultiplier가 됨
        // h = 1/2 * g * t^2 => t_up = sqrt(2h/g)
        // t_down = sqrt(2h / (g * fallMultiplier)) = t_up / sqrt(fallMultiplier)
        float timeToLand = timeToPeak / Mathf.Sqrt(fallMultiplier);
        
        float totalJumpTime = timeToPeak + timeToLand;

        // 2. 애니메이션 클립 길이 찾기
        float animationLength = 0f;
        RuntimeAnimatorController ac = animator.runtimeAnimatorController;
        if (ac != null)
        {
            foreach (AnimationClip clip in ac.animationClips)
            {
                // "Jump"라는 이름이 포함된 클립을 찾습니다. (대소문자 무시)
                if (clip.name.IndexOf("Jump", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    animationLength = clip.length;
                    break;
                }
            }
        }

        if (animationLength > 0)
        {
            // 3. 속도 배수 계산 (애니메이션 길이 / 실제 점프 시간)
            float jumpSpeedMultiplier = animationLength / totalJumpTime;
            
            // Animator에 파라미터 전달 (Animator에 "JumpSpeed" 파라미터가 있어야 함)
            animator.SetFloat("JumpSpeed", jumpSpeedMultiplier);
            
        }
    }
}

// Vector3의 각 요소를 절대값으로 만드는 확장 메서드
public static class Vector3Extensions
{
    public static Vector3 Abs(this Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
