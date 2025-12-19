using UnityEngine;

/// <summary>
/// 플레이어가 밟으면 슈퍼 점프를 발동시키는 점프 패드 스크립트입니다.
/// </summary>
public class JumpPad : MonoBehaviour
{
    [Header("점프 패드 설정")]
    [Tooltip("플레이어의 기본 점프력에 곱할 배율입니다. (예: 2f는 2배 점프)")]
    public float superJumpMultiplier = 2.5f;
    [Tooltip("플레이어 오브젝트의 태그 (예: 'Player')")]
    public string playerTag = "Player";

    private AudioSource audioSource; // 점프 패드 사운드 재생용
    [Tooltip("점프 패드가 활성화될 때 재생할 사운드")]
    public AudioClip jumpSound;

    void Start()
    {
        // Collider가 Trigger로 설정되어 있는지 확인
        Collider col = GetComponent<Collider>();
        if (col == null || !col.isTrigger)
        {
            // Debug.LogWarning($"JumpPad '{gameObject.name}': Collider 컴포넌트가 없거나 Trigger로 설정되어 있지 않습니다. 점프 패드가 작동하지 않을 수 있습니다.");
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerMovement playerMovement = other.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.ApplyJumpForce(superJumpMultiplier); // 플레이어에게 슈퍼 점프 적용
                if (audioSource != null && jumpSound != null)
                {
                    audioSource.PlayOneShot(jumpSound); // 점프 사운드 재생
                }                
            }
        }
    }
}