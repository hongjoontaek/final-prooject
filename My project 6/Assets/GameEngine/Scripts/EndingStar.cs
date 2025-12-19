using UnityEngine;
using System.Collections; // 코루틴을 사용하기 위해 추가

/// <summary>
/// 플레이어가 터치하면 게임 엔딩을 트리거하는 별 오브젝트 스크립트입니다.
/// </summary>
public class EndingStar : MonoBehaviour
{
    [Tooltip("플레이어 오브젝트의 태그 (예: 'Player')")]
    public string playerTag = "Player";

    // 엔딩 시퀀스가 한 번만 실행되도록 하기 위한 플래그

    [Header("화면 밝기 효과")]
    [Tooltip("화면이 완전히 밝아지는 데 걸리는 시간")]
    public float fadeToWhiteDuration = 3f;
    [Tooltip("화면이 완전히 밝아질 때의 색상")]
    public Color fadeColor = Color.white;

    private bool hasTriggered = false;

    // 플레이어가 이 별 오브젝트의 트리거에 닿았을 때 호출됩니다.
    void OnTriggerEnter(Collider other)
    {
        // 아직 엔딩이 트리거되지 않았고, 닿은 오브젝트가 "Player" 태그를 가지고 있다면
        if (!hasTriggered && other.CompareTag(playerTag))
        {
            Debug.Log($"<color=green>EndingStar: Player '{other.name}' touched the star! Initiating fade to white.</color>");
            hasTriggered = true; // 엔딩이 트리거되었음을 표시

            // GameManager의 엔딩 시퀀스 시작 함수를 호출하는 대신, 화면 밝기 효과를 시작합니다.
            // GameManager.Instance.StartEndingSequence(); // 이 줄은 이제 호출하지 않습니다.

            // 메인 카메라를 찾아서 화면 밝기 코루틴 시작
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                StartCoroutine(FadeScreenToColor(mainCam, fadeColor, fadeToWhiteDuration));
            }
            else
            {
                Debug.LogError("EndingStar: Main Camera를 찾을 수 없습니다. 화면 밝기 효과를 적용할 수 없습니다.");
                Debug.LogWarning("EndingStar: Main Camera를 찾을 수 없습니다. 화면 밝기 효과를 적용할 수 없습니다.");
            }
        }
    }

    // 화면을 점진적으로 밝게 만드는 코루틴
    private IEnumerator FadeScreenToColor(Camera cam, Color targetColor, float duration)
    {
        Color startColor = cam.backgroundColor;
        float timer = 0f;
        while (timer < duration)
        {
            cam.backgroundColor = Color.Lerp(startColor, targetColor, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
        cam.backgroundColor = targetColor; // 최종 색상으로 설정
        // 여기에 게임 종료 또는 씬 전환 로직을 추가할 수 있습니다.
        // 예: GameManager.Instance.RestartGame();
    }
}