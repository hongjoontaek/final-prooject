using UnityEngine;
using System.Collections; // 코루틴을 사용하기 위해 필요합니다.

/// <summary>
/// 플레이어가 밟으면 일정 시간 후 부서지는 발판 스크립트입니다.
/// </summary>
public class BreakablePlatform : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("플레이어가 밟은 후 발판이 부서지기까지의 시간 (초)")]
    public float breakDelay = 1.0f;
    [Tooltip("플레이어 오브젝트의 태그 (예: 'Player')")]
    public string playerTag = "Player";

    [Header("부서지는 모션")]
    [Tooltip("발판이 부서지기 전에 흔들리는 정도")]
    [Range(0f, 0.5f)]
    public float shakeMagnitude = 0.1f;
    [Tooltip("발판이 흔들리는 시간 (breakDelay보다 작거나 같아야 합니다)")]
    public float shakeDuration = 0.5f;
    [Tooltip("발판이 부서질 때 재생할 사운드")]
    public AudioClip breakSound;

    private Vector3 initialPosition;    // 발판의 초기 위치 (리스폰 시 사용)
    private Quaternion initialRotation; // 발판의 초기 회전 (리스폰 시 사용)
    private Vector3 initialLocalPosition; // 흔들림을 위해 초기 로컬 위치 저장
    private bool isBroken = false; // 현재 발판이 부서진 상태인지
    private Renderer[] platformRenderers; // 발판의 모든 렌더러 컴포넌트
    private Collider platformCollider; // 발판의 콜라이더 컴포넌트
    private AudioSource audioSource; // 오디오 재생을 위한 AudioSource

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialLocalPosition = transform.localPosition; // 로컬 위치 저장

        // 모든 렌더러와 콜라이더 컴포넌트 가져오기
        platformRenderers = GetComponentsInChildren<Renderer>(); // 모든 하위 렌더러를 가져옴
        platformCollider = GetComponent<Collider>();
        if (platformRenderers.Length == 0 || platformCollider == null) // 렌더러가 하나도 없거나 콜라이더가 없으면 에러
        {
            // Debug.LogError("BreakablePlatform: 렌더러 또는 콜라이더 컴포넌트를 찾을 수 없습니다! 스크립트를 비활성화합니다.");
            enabled = false; // 스크립트 비활성화
        }

        // AudioSource 컴포넌트 가져오기 (없으면 추가)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // 자동으로 재생되지 않도록 설정
        }
    }

    // 플레이어가 이 발판에 닿았을 때 호출됩니다.
    private void OnCollisionEnter(Collision collision)
    {
        // 플레이어 태그를 가진 오브젝트와 충돌했고, 아직 부서지지 않은 상태라면
        if (collision.gameObject.CompareTag(playerTag) && !isBroken)
        {
            StartCoroutine(BreakAndRespawn());
        }
    }

    // 발판을 부수고 다시 나타나게 하는 코루틴입니다.
    private IEnumerator BreakAndRespawn()
    {
        isBroken = true; // 부서진 상태로 변경
        // Debug.Log($"<color=yellow>발판 '{gameObject.name}'이 부서지기 시작합니다.</color>");

        // 부서지는 사운드 재생
        if (audioSource != null && breakSound != null)
        {
            audioSource.PlayOneShot(breakSound);
        }

        float timer = 0f;
        while (timer < breakDelay)
        {
            // shakeDuration 동안만 흔들림 적용
            if (timer < shakeDuration)
            {
                transform.localPosition = initialLocalPosition + Random.insideUnitSphere * shakeMagnitude;
            }
            timer += Time.deltaTime;
            yield return null; // 다음 프레임까지 대기
        }
        transform.localPosition = initialLocalPosition; // 흔들림 후 원래 위치로 복귀

        // 발판을 비활성화하여 사라지게 합니다.
        // [수정] GameObject를 비활성화하는 대신 렌더러와 콜라이더만 비활성화합니다.
        foreach (Renderer r in platformRenderers) // 모든 렌더러 비활성화
        {
            if (r != null) r.enabled = false;
        }
        if (platformCollider != null) platformCollider.enabled = false;
        // Debug.Log($"<color=green>발판 '{gameObject.name}'이 사라졌습니다.</color>");

        // 발판이 부서진 후에는 외부에서 ResetPlatform()을 호출하여 다시 나타나게 합니다.
        // 자동 리스폰 로직은 제거되었습니다.
    }

    /// <summary>
    /// 발판을 초기 상태로 되돌리고 다시 활성화합니다.
    /// 게임 재시작 등 외부 이벤트에 의해 호출됩니다.
    /// </summary>
    public void ResetPlatform()
    {
        StopAllCoroutines(); // 혹시 진행 중인 코루틴이 있다면 중지
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        foreach (Renderer r in platformRenderers) // 모든 렌더러 다시 활성화
        {
            if (r != null) r.enabled = true;
        }
        if (platformCollider != null) platformCollider.enabled = true; // 콜라이더 다시 활성화
        isBroken = false; // 부서진 상태 해제
        // Debug.Log($"<color=green>발판 '{gameObject.name}'이 초기화되어 다시 나타났습니다.</color>");
    }
}