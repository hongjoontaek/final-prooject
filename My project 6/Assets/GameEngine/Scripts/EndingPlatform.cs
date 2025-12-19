using UnityEngine;

/// <summary>
/// 플레이어가 밟으면 게임 엔딩을 트리거하는 발판 스크립트입니다.
/// </summary>
public class EndingPlatform : MonoBehaviour
{
    [Tooltip("플레이어 오브젝트의 태그 (예: 'Player')")]
    public string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        // 충돌한 오브젝트가 플레이어 태그를 가지고 있는지 확인합니다.
        if (other.CompareTag(playerTag))
        {
            // GameManager 인스턴스가 존재하고, 아직 게임이 종료되지 않았다면 엔딩을 트리거합니다.
            if (GameManager.Instance != null && !GameManager.Instance.IsGamePaused)
            {                
                GameManager.Instance.StartEndingSequence(); // TriggerGameEnding 대신 StartEndingSequence 호출
            }
            else if (GameManager.Instance == null)
            {                
                // Debug.LogError("GameManager 인스턴스를 찾을 수 없습니다! GameManager가 씬에 있는지 확인해주세요.");
            }
        }
    }
}