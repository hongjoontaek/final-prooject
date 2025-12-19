using UnityEngine;

/// <summary>
/// 두 지점 사이를 왕복하는 움직이는 발판 스크립트입니다.
/// </summary>
public class MovingPlatform : MonoBehaviour
{
    [Header("이동 경로 설정 (초기 위치 기준 오프셋)")]
    [Tooltip("발판의 초기 위치를 기준으로 한 시작 지점 오프셋")]
    public Vector3 startOffset = Vector3.zero; // 기본값은 초기 위치
    [Tooltip("발판의 초기 위치를 기준으로 한 끝 지점 오프셋")]
    public Vector3 endOffset;
    [Tooltip("발판의 이동 속도")]
    public float speed = 2f;
    [Tooltip("발판이 각 끝 지점에서 대기하는 시간")]
    public float delayAtEnds = 1f;

    [Header("플레이어 설정")]
    [Tooltip("플레이어 오브젝트의 태그 (예: 'Player')")]
    public string playerTag = "Player";

    private Vector3 initialWorldPosition; // 발판의 초기 월드 위치
    private Vector3 worldStartPoint;      // 실제 이동 시작 월드 좌표
    private Vector3 worldEndPoint;        // 실제 이동 끝 월드 좌표
    private Vector3 nextTarget;           // 다음 목표 월드 좌표
    private float waitTimer;    // 대기 타이머
    private bool movingToEnd = true; // endPoint로 이동 중인지 여부

    void Start()
    {
        initialWorldPosition = transform.position; // 현재 월드 위치를 초기 위치로 저장
        worldStartPoint = initialWorldPosition + startOffset; // 초기 위치 + 오프셋으로 실제 시작점 계산
        worldEndPoint = initialWorldPosition + endOffset;     // 초기 위치 + 오프셋으로 실제 끝점 계산
        
        nextTarget = worldEndPoint; // 초기 목표는 끝점으로 설정
        waitTimer = delayAtEnds; // 초기 대기 시간 설정

        // Rigidbody가 있는지 확인하고, 없으면 추가하고 Kinematic으로 설정
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true; // 물리 엔진에 의해 움직이지 않도록 설정
        rb.useGravity = false; // 중력의 영향을 받지 않도록 설정

        // Collider가 있는지 확인
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            // Debug.LogWarning($"MovingPlatform '{gameObject.name}': Collider 컴포넌트를 찾을 수 없습니다. BoxCollider를 추가합니다.");
            gameObject.AddComponent<BoxCollider>();
        }
    }

    void FixedUpdate()
    {
        // 게임 플레이가 활성화되었을 때만 움직입니다.
        if (GameManager.Instance != null && !GameManager.Instance.IsGameplayActive)
        {
            return;
        }

        // 목표 지점에 도달했는지 확인
        if (Vector3.Distance(transform.position, nextTarget) < 0.01f)
        {
            // 목표 지점에 도달했으면 대기 타이머 시작
            if (waitTimer > 0)
            {
                waitTimer -= Time.fixedDeltaTime;
            }
            else
            {
                // 대기 시간이 끝나면 다음 목표 지점 설정
                movingToEnd = !movingToEnd; // 방향 전환
                nextTarget = movingToEnd ? worldEndPoint : worldStartPoint;
                waitTimer = delayAtEnds; // 대기 타이머 초기화
            }
        }
        else
        {
            // 목표 지점으로 이동
            // Rigidbody.MovePosition을 사용하여 물리적으로 이동시킵니다.
            Vector3 newPosition = Vector3.MoveTowards(transform.position, nextTarget, speed * Time.fixedDeltaTime);
            GetComponent<Rigidbody>().MovePosition(newPosition);
        }
    }

    // 플레이어가 발판 위에 올라섰을 때
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            // 플레이어를 발판의 자식으로 만듭니다. 이렇게 하면 플레이어가 발판과 함께 움직입니다.
            collision.transform.SetParent(transform);
            // Debug.Log($"<color=green>플레이어가 발판 '{gameObject.name}'에 올라섰습니다.</color>");
        }
    }

    // 플레이어가 발판에서 내려왔을 때
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag(playerTag))
        {
            // 플레이어의 부모를 해제합니다.
            collision.transform.SetParent(null);
            // Debug.Log($"<color=red>플레이어가 발판 '{gameObject.name}'에서 내려왔습니다.</color>");
        }
    }

    // 에디터에서 발판의 경로를 시각적으로 보여줍니다.
    void OnDrawGizmos()
    {
        // 현재 발판의 위치를 기준으로 오프셋을 더하여 경로를 그립니다.
        // 이렇게 하면 에디터에서 발판을 옮겨도 경로가 함께 이동합니다.
        Gizmos.color = Color.yellow;
        Vector3 currentWorldStart = transform.position + startOffset;
        Vector3 currentWorldEnd = transform.position + endOffset;
        Gizmos.DrawLine(currentWorldStart, currentWorldEnd);
        Gizmos.DrawSphere(currentWorldStart, 0.2f);
        Gizmos.DrawSphere(currentWorldEnd, 0.2f);
    }
}