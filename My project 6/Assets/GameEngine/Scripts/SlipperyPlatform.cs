using UnityEngine;

/// <summary>
/// 플레이어가 밟고 가만히 있으면 특정 방향으로 미끄러지게 하는 발판 스크립트입니다.
/// </summary>
public class SlipperyPlatform : MonoBehaviour
{
    public enum SlideDirection { Left, Right, Forward, Backward }

    [Header("미끄러운 발판 설정")]
    [Tooltip("플레이어가 미끄러질 방향 (카메라 시점 기준)")]
    public SlideDirection slideDirection = SlideDirection.Left;
    [Tooltip("플레이어 오브젝트의 태그 (예: 'Player')")]
    public string playerTag = "Player";

    void Start()
    {
        Debug.Log($"<color=green>SlipperyPlatform '{gameObject.name}' 시작됨.</color>", this);
        // 이 스크립트는 더 이상 CameraRotation을 직접 참조할 필요가 없습니다.
        // PlayerMovement 스크립트의 externalHorizontalInput을 제어하여 플레이어의 이동을 유도합니다.
    }

    // 플레이어가 이 발판 위에 머무는 동안 계속 호출됩니다.
    private void OnCollisionStay(Collision collision)
    {
        // Debug.Log($"<color=yellow>SlipperyPlatform '{gameObject.name}': OnCollisionStay 감지. 충돌 오브젝트: {collision.gameObject.name}</color>", this);

        // 플레이어 태그를 가진 오브젝트와 충돌 중이라면
        // [수정] 플레이어의 Rigidbody는 더 이상 직접 힘을 가하지 않으므로 필요 없습니다.
        if (collision.gameObject.CompareTag(playerTag))
        {
            // Debug.Log($"<color=yellow>SlipperyPlatform '{gameObject.name}': 플레이어 태그 일치. 오브젝트: {collision.gameObject.name}</color>", this);
            PlayerMovement playerMovement = collision.gameObject.GetComponent<PlayerMovement>();

            if (playerMovement != null) // PlayerMovement 스크립트가 플레이어 오브젝트에 있다면
            {
                // 플레이어의 externalHorizontalInput을 설정하여 미끄러지는 효과를 줍니다.
                // 미끄러지는 방향에 따라 externalHorizontalInput 또는 externalVerticalInput을 설정합니다.
                playerMovement.externalHorizontalInput = 0f; // 다른 방향의 입력은 초기화
                playerMovement.externalVerticalInput = 0f; // 다른 방향의 입력은 초기화

                switch (slideDirection)
                {
                    case SlideDirection.Left:
                        playerMovement.externalHorizontalInput = -1f; // 왼쪽
                        break;
                    case SlideDirection.Right:
                        playerMovement.externalHorizontalInput = 1f; // 오른쪽
                        break;
                    case SlideDirection.Forward:
                        playerMovement.externalVerticalInput = 1f; // 앞 (카메라 기준)
                        break;
                    case SlideDirection.Backward:
                        playerMovement.externalVerticalInput = -1f; // 뒤 (카메라 기준)
                        break;
                }
                Debug.Log($"<color=cyan>SlipperyPlatform '{gameObject.name}': 플레이어를 {slideDirection} 방향으로 이동시킵니다. (externalHorizontalInput: {playerMovement.externalHorizontalInput}, externalVerticalInput: {playerMovement.externalVerticalInput})</color>", this);
            }
            else
            {
                Debug.LogWarning($"<color=red>SlipperyPlatform '{gameObject.name}': 플레이어 '{collision.gameObject.name}'에서 PlayerMovement 스크립트를 찾을 수 없습니다!</color>", this);
            }
        }
        // else
        // {
        //     Debug.Log($"<color=yellow>SlipperyPlatform '{gameObject.name}': 태그 불일치. 충돌 오브젝트 태그: {collision.gameObject.tag}</color>", this);
        // }
    }

    // 플레이어가 이 발판에서 벗어났을 때 호출됩니다.
    private void OnCollisionExit(Collision collision)
    {
        // 플레이어 태그를 가진 오브젝트가 충돌에서 벗어났다면
        if (collision.gameObject.CompareTag(playerTag))
        {   
            // Debug.Log($"<color=yellow>SlipperyPlatform '{gameObject.name}': OnCollisionExit 감지. 플레이어 태그 일치. 오브젝트: {collision.gameObject.name}</color>", this);
            PlayerMovement playerMovement = collision.gameObject.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                // 플레이어의 externalHorizontalInput과 externalVerticalInput을 0으로 초기화하여 미끄러지는 효과를 중지합니다.
                playerMovement.externalHorizontalInput = 0f;
                playerMovement.externalVerticalInput = 0f;
                Debug.Log($"<color=cyan>SlipperyPlatform '{gameObject.name}': 플레이어가 발판에서 벗어났습니다. externalHorizontalInput을 0으로 초기화합니다.</color>", this);
            } else {
                Debug.LogWarning($"<color=red>SlipperyPlatform '{gameObject.name}': OnCollisionExit에서 플레이어 '{collision.gameObject.name}'의 PlayerMovement 스크립트를 찾을 수 없습니다!</color>", this);
            }
        }
    }
}