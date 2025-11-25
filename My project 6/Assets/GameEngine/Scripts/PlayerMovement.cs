using UnityEngine;
using UnityEngine.InputSystem;

// MonoBehaviour를 상속받습니다.
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f; // 플레이어 이동 속도
    public float jumpForce = 7f; // 플레이어 점프 힘
    public LayerMask wallLayer; // 벽으로 인식할 레이어 (차원 접힘 방지용)

    [Header("Ground Check")]
    public LayerMask groundLayer; // 바닥으로 인식할 레이어 (Unity 에디터에서 설정)
    public float dimensionFoldDepth = 50f; // 차원 접힘을 감지할 깊이
    public float groundCheckDistance = 1.1f; // 바닥 체크를 위한 레이캐스트 거리

    private Rigidbody rb; // 플레이어의 Rigidbody 컴포넌트
    private CameraRotation camRotation; // 메인 카메라에 붙어있는 CameraRotation 스크립트 참조
    private bool isGrounded; // 플레이어가 바닥에 닿아있는지 여부
    private bool canMove = true; // 플레이어가 움직일 수 있는지 여부
    private bool jumpRequested = false; // 점프 요청을 저장할 변수

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

        // 3. 게임 플레이 시 마우스 커서를 숨기고 중앙에 고정
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 매 프레임마다 호출 (주로 입력 감지 및 논리 처리)
    void Update()
    {
        // 1. 바닥 체크 로직 호출 (기본 체크 및 차원 접힘 체크 포함)
        CheckGrounded(); // 새로운 바닥 체크 함수 호출
        // (확인용) Scene 뷰에 빨간색 레이저를 그려 바닥 체크가 어디서 이루어지는지 시각적으로 보여줍니다.
        Debug.DrawRay(transform.position, Vector3.down * groundCheckDistance, Color.red); 

        // 2. 점프 (바닥에 있을 때만 점프 가능)
        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            Debug.Log("점프 입력 감지! (isGrounded: " + isGrounded + ")");
            jumpRequested = true; // 점프 요청을 기록
        }
    }

     private void CheckGrounded()
     {
         // 1. 기본 바닥 체크 (플레이어 바로 아래)
         if (Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer))
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
 
             Vector3 rayStart = transform.position - (targetPos - transform.position).normalized * 0.5f;
             float distance = Vector3.Distance(rayStart, targetPos);
             Vector3 direction = (targetPos - rayStart).normalized;
 
             if (!Physics.Raycast(rayStart, direction, distance, wallLayer))
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
        // canMove가 false이면 수평 이동을 막고 함수를 종료합니다.
        if (!canMove)
        {
            // 수직 속도(중력, 점프)는 유지하면서 수평 속도만 0으로 만듭니다.
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
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

                    Vector3 rayStart = transform.position - (targetPos - transform.position).normalized * 0.5f;
                    float distance = Vector3.Distance(rayStart, targetPos);
                    Vector3 direction = (targetPos - rayStart).normalized;

                    if (!Physics.Raycast(rayStart, direction, distance, wallLayer)) // 벽에 안가려졌다면
                    {
                        float distToCam = Vector3.Distance(targetPos, camRotation.transform.position);
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
    }

    /// <summary>
    /// 지정된 시간(초) 동안 플레이어의 움직임을 비활성화합니다.
    /// </summary>
    /// <param name="duration">움직임을 비활성화할 시간(초)</param>
    public void DisableMovementForDuration(float duration)
    {
        StartCoroutine(DisableMovementCoroutine(duration));
    }

    private System.Collections.IEnumerator DisableMovementCoroutine(float duration) {
        canMove = false;
        yield return new WaitForSeconds(duration);
        canMove = true;
    }
}
