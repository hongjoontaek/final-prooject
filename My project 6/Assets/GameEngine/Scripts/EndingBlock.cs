using UnityEngine;

public class EndingBlock : MonoBehaviour
{
    // 엔딩 시퀀스가 한 번만 실행되도록 하기 위한 플래그
    private bool hasTriggered = false;

    // 플레이어가 이 블록에 닿았을 때 호출됩니다.
    void OnTriggerEnter(Collider other)
    {
        // 아직 엔딩이 트리거되지 않았고, 닿은 오브젝트가 "Player" 태그를 가지고 있다면
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true; // 엔딩이 트리거되었음을 표시
            

            // GameManager의 엔딩 시퀀스 시작 함수를 호출합니다.
            GameManager.Instance.StartEndingSequence();
        }
    }

    // 이 블록이 트리거로 작동하도록 설정하기 위해 필요합니다.
    // 이 스크립트를 오브젝트에 추가할 때 자동으로 Collider 컴포넌트의 Is Trigger를 true로 설정합니다.
    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
            // Debug.Log($"EndingBlock: Collider '{col.name}'의 Is Trigger를 true로 설정했습니다.");
        }
        else
        {
            Debug.LogWarning("EndingBlock: Collider 컴포넌트를 찾을 수 없습니다. 수동으로 추가하고 Is Trigger를 true로 설정해주세요.");
        }
    }
}
