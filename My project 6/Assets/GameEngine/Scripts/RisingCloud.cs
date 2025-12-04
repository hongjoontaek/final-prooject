using UnityEngine;

/// <summary>
/// 아래에서 위로 올라오며 시간 제한 역할을 하는 구름 스크립트
/// </summary>
public class RisingCloud : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("구름이 올라오는 속도")]
    public float riseSpeed = 0.5f;

    private Vector3 initialPosition; // 구름의 초기 위치
    private Transform playerTransform; // 플레이어의 Transform 참조
    private bool isPlayerCaught = false; // 플레이어가 구름에 잡혔는지 여부 (중복 호출 방지)

    void Start()
    {
        // 초기 위치 저장
        initialPosition = transform.position;

        // "Player" 태그를 가진 오브젝트를 찾아서 참조 저장
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
        }
        else
        {
            Debug.LogError("씬에서 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다! 플레이어에 'Player' 태그를 설정해주세요.");
            enabled = false; // 플레이어가 없으면 스크립트 비활성화
        }
    }

    void Update()
    {
        // [수정] GameManager가 게임 플레이가 활성화되었다고 알려줄 때만 움직입니다.
        if (GameManager.Instance != null && GameManager.Instance.IsGameplayActive)
        {
            // 매 프레임 위쪽으로 이동
            transform.Translate(Vector3.up * riseSpeed * Time.deltaTime);
        }

        // 플레이어가 아직 잡히지 않았고, 플레이어 참조가 유효할 때
        if (!isPlayerCaught && playerTransform != null)
        {
            // 구름의 Y좌표가 플레이어의 Y좌표보다 높아지면
            if (transform.position.y > playerTransform.position.y)
            {
                isPlayerCaught = true; // 잡혔다고 표시
                Debug.Log("구름이 플레이어를 따라잡았습니다!");

                // GameManager에 구름으로 인한 사망 처리를 요청
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.HandleCloudDeath();
                }
            }
        }
    }

    // 구름의 위치를 초기화하는 함수 (GameManager에서 호출)
    public void ResetCloud()
    {
        transform.position = initialPosition;
        isPlayerCaught = false; // 플레이어 잡힘 상태 초기화
    }
}
