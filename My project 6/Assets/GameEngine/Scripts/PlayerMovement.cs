using UnityEngine;
using UnityEngine.InputSystem;
using TMPro; // TextMeshPro를 사용하기 위해 이 줄을 추가합니다.

// MonoBehaviour를 상속받습니다.
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f; // 플레이어 이동 속도
    public float jumpForce = 7f; // 플레이어 점프 힘
    [Header("Jump Physics")]
    public float fallMultiplier = 2f; // 떨어질 때 적용할 중력 배수 (더 빨리 떨어지게 함)
    public float lowJumpMultiplier = 2f; // 짧게 점프할 때 적용할 중력 배수
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
    public Material silhouetteMaterial; // 벽 뒤에 있을 때 사용할 실루엣 머티리얼
    public Transform visibilityCheckTarget; // 가려졌는지 확인할 플레이어의 부위 (보통 머리나 몸통 중앙)
    private Material normalMaterial; // 플레이어의 원래 머티리얼
    private Renderer playerRenderer; // 플레이어 모델의 Renderer 컴포넌트

    [Header("Fall Damage")]
    public float deathYLevel = 0f; // 이 Y축 높이 이하로 떨어지면 사망 처리
    public float maxTeleportDistance = 50f; // 자동 붙이기 기능의 최대 순간이동 거리

    [Header("Interaction")]
    public GameObject interactionPromptUI; // "Z키를 눌러 읽기" UI 오브젝트
    public TextMeshProUGUI narrationTextUI; // [수정] TextMeshPro 텍스트를 받도록 변경
    public GameObject narrationPanelUI; // 나레이션 텍스트를 담고 있는 패널
    private InteractableSign currentInteractable; // 현재 상호작용 가능한 오브젝트
    private bool isReading = false; // 현재 나레이션을 읽고 있는지 상태

    private Rigidbody rb; // 플레이어의 Rigidbody 컴포넌트
    private CameraRotation camRotation; // 메인 카메라에 붙어있는 CameraRotation 스크립트 참조
    private bool isGrounded; // 플레이어가 바닥에 닿아있는지 여부
    private bool jumpRequested = false; // 점프 요청을 저장할 변수
    private bool dropRequested = false; // 아래로 내려가기 요청을 저장할 변수
    private bool isDropping = false; // 현재 아래로 내려가는 중인지 상태를 저장할 변수
    private bool isSilhouetteMode = false; // 현재 실루엣 모드인지 상태를 저장
    private Vector3 initialPosition; // 초기 스폰 위치를 저장할 변수
    private bool wasCameraRotating = false; // 이전 프레임에서 카메라가 회전 중이었는지 확인

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

        // 3. [새로운 기능] 플레이어의 렌더러와 원래 머티리얼을 가져옵니다.
        playerRenderer = GetComponentInChildren<Renderer>();
        if (playerRenderer != null)
        {
            normalMaterial = playerRenderer.material;
        }
        else
        {
            Debug.LogError("플레이어 모델의 Renderer를 찾을 수 없습니다! 자식 오브젝트에 모델이 있는지 확인해주세요.");
        }
        // visibilityCheckTarget이 설정되지 않았다면, 플레이어 자기 자신을 타겟으로 설정합니다.
        if (visibilityCheckTarget == null)
            visibilityCheckTarget = transform;

        // 3. 게임 플레이 시 마우스 커서를 숨기고 중앙에 고정
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 4. [새로운 기능] 초기 위치 저장
        initialPosition = transform.position;
    }

    // 매 프레임마다 호출 (주로 입력 감지 및 논리 처리)
    void Update()
    {
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

        // 1. 바닥 체크 로직 호출 (기본 체크 및 차원 접힘 체크 포함)
        CheckGrounded(); // 새로운 바닥 체크 함수 호출
        // (확인용) Scene 뷰에 빨간색 레이저를 그려 바닥 체크가 어디서 이루어지는지 시각적으로 보여줍니다.
        Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, Color.red); 

        // 2. 점프 (바닥에 있을 때만 점프 가능)
        bool isDownPressed = Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed;

        // 카메라가 회전 중이 아닐 때만 점프 입력을 받습니다.
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded && !camRotation.IsRotating)
        {
            // [로그 1] 점프 입력이 들어왔을 때, 아래 키가 눌렸는지 확인합니다.
            Debug.Log($"[입력 감지] 점프 시도! isDownPressed: {isDownPressed}");

            // 아래 방향키를 누르고 점프하면 '아래로 내려가기' 요청
            if (isDownPressed)
            {
                dropRequested = true;
                Debug.Log("<color=orange>[요청] 아래로 내려가기(dropRequested)가 true로 설정됨.</color>");
            }
            else // 그렇지 않으면 일반 점프 요청
            {
                Debug.Log("점프 입력 감지! (isGrounded: " + isGrounded + ")");
                jumpRequested = true; // 점프 요청을 기록
            }
        }

        // 3. 벽 잡기 체크 (공중에 있고, 위로 올라가는 중일 때만)
        CheckLedge();

        // 4. [새로운 기능] 추락 및 착지 감지
        HandleFallDetection();
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
         // 1. 기본 바닥 체크 (플레이어 바로 아래)
         bool basicHit = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);
         if (basicHit)
         {
             isGrounded = true;
             return;
         }
 
         // 2. "차원 접힘" 바닥 체크 (공중에 있을 때만 작동)
         Vector3 boxCenter = transform.position + Vector3.down * 0.1f; // 플레이어 발밑에서 시작
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
         RaycastHit[] hits = Physics.BoxCastAll(boxCenter, halfExtents, Vector3.down, Quaternion.identity, maxDistance, groundLayer);
 
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
 
         isGrounded = false;
     }

    // 고정된 물리 프레임마다 호출 (물리 계산에 적합)
    void FixedUpdate()
    {
        // 벽 오르기 중에는 물리 계산을 무시합니다.
        if (isClimbing)
        {
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
            Debug.Log("<color=yellow>[FixedUpdate] 아래로 내려가기 처리 시작!</color>");
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
        float moveInput = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            moveInput = -1f;
        }
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            moveInput = 1f;
        }

        // [수정] 플레이어가 처음으로 움직였을 때 GameManager에 게임 시작을 알립니다.
        if (moveInput != 0f && GameManager.Instance != null && !GameManager.Instance.IsGameplayActive)
        {
            GameManager.Instance.StartGameplay();
        }

        // 게임이 아직 시작되지 않았다면 움직이지 않습니다.
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive) moveInput = 0f;

        // 2. 현재 Rigidbody의 속도를 가져와 Y축 속도(중력, 점프)는 그대로 유지
        // Y축 속도는 그대로 두고, X와 Z축 속도만 새로 계산합니다.
        float yVelocity = rb.linearVelocity.y;

        // --- 여기가 FEZ 로직의 핵심 ---
        // 3. 카메라 뷰에 따라 '좌우' 입력을 X축 또는 Z축 속도로 변환
        //    카메라가 회전하면 플레이어의 '좌우' 개념도 카메라 시점에 맞춰 바뀝니다.
        
        Vector3 velocity = Vector3.zero;

        // CameraRotation 스크립트의 currentView 값에 따라 이동 방향을 결정
        switch (camRotation.currentView)
        {
            case CameraRotation.CameraView.Front: // 카메라가 정면(Z- 방향)을 볼 때
                velocity.x = moveInput * speed; // '오른쪽'은 +X축 방향
                break;
            case CameraRotation.CameraView.Right: // 카메라가 우측(X+ 방향)을 볼 때
                velocity.z = -moveInput * speed; // '오른쪽'은 -Z축 방향
                break;
            case CameraRotation.CameraView.Back:  // 카메라가 후면(Z+ 방향)을 볼 때
                velocity.x = -moveInput * speed; // '오른쪽'은 -X축 방향
                break;
            case CameraRotation.CameraView.Left:  // 카메라가 좌측(X- 방향)을 볼 때
                velocity.z = moveInput * speed; // '오른쪽'은 +Z축 방향
                break;
        }

        // Y축 속도를 다시 합쳐줍니다.
        velocity.y = yVelocity;

        // --- [새로운 로직] 지상에서 수평 이동 시 더 나은 발판으로 자동 이동 ---
        if (isGrounded && moveInput != 0)
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
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpRequested = false; // 요청을 처리했으므로 초기화
        }

        // --- [새로운 로직] 더 나은 점프 물리를 위한 처리 ---
        // 1. 떨어질 때 더 빨리 떨어지도록 중력을 강화합니다.
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        // 2. 점프 키를 짧게 누르면 점프 높이가 낮아지도록 합니다.
        else if (rb.linearVelocity.y > 0 && !Keyboard.current.spaceKey.isPressed)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
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
        Debug.Log($"<color=cyan>[순간이동] {transform.position} 위치로 이동!</color>");
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
        Debug.Log("<color=green>발판 자동 붙이기 기능 다시 활성화.</color>");
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

        Debug.Log("<color=lightblue>벽 오르기 시작!</color>");

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

        Debug.Log("<color=lightblue>벽 오르기 완료!</color>");
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
        if (playerRenderer == null || silhouetteMaterial == null || normalMaterial == null)
            return;

        bool isCameraRotatingNow = camRotation.IsRotating;

        // 1. 카메라 회전이 '끝나는' 시점을 감지합니다.
        if (wasCameraRotating && !isCameraRotatingNow)
        {
            isSilhouetteMode = IsPlayerObscured();
        }

        // 2. 실루엣 모드일 때, 플레이어가 벽 뒤에서 '나왔는지' 확인합니다.
        if (isSilhouetteMode)
        {
            if (!IsPlayerObscured())
            {
                isSilhouetteMode = false; // 벽 뒤에서 나왔으므로 실루엣 모드를 끕니다.
            }
        }

        // 3. 최종 'isSilhouetteMode' 상태에 따라 머티리얼을 적용합니다.
        if (isSilhouetteMode)
        {
            playerRenderer.material = silhouetteMaterial;
        }
        else
        {
            playerRenderer.material = normalMaterial;
        }

        // 다음 프레임을 위해 현재 카메라 회전 상태를 저장합니다.
        wasCameraRotating = isCameraRotatingNow;
    }

    // 플레이어가 벽에 가려졌는지 확인하는 헬퍼 함수
    /// <summary>
    /// 플레이어가 벽에 가려졌는지 확인하는 헬퍼 함수
    /// </summary>
    /// <returns>가려졌으면 true, 아니면 false</returns>
    private bool IsPlayerObscured()
    {
        Vector3 direction = visibilityCheckTarget.position - camRotation.transform.position;
        float playerDistance = direction.magnitude;

        // 카메라와 플레이어 사이에 Wall이 있는지 단순하게 확인합니다.
        return Physics.Raycast(camRotation.transform.position, direction, playerDistance, wallLayer);
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
        Debug.LogWarning($"사망! 원인: {cause}");

        // 초기 위치로 플레이어를 리스폰시킵니다.
        transform.position = initialPosition;
        // 추락하던 속도를 0으로 초기화하여 리스폰 후 바로 다시 떨어지는 것을 방지합니다.
        rb.linearVelocity = Vector3.zero;

        // GameManager를 찾아 게임 오버 UI를 띄웁니다.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
        }

        // [수정] 카메라의 위치와 회전도 함께 초기화합니다.
        if (camRotation != null)
        {
            camRotation.ResetCamera();
        }
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
            interactionPromptUI.SetActive(true); // "Z키" 프롬프트 UI 보이기
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
            interactionPromptUI.SetActive(false); // "Z키" 프롬프트 UI 숨기기
        }
    }
}

// Vector3의 각 요소를 절대값으로 만드는 확장 메서드
public static class Vector3Extensions
{
    public static Vector3 Abs(this Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
