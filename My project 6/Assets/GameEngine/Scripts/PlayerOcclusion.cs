// PlayerOcclusion.cs
using UnityEngine;
using System.Collections.Generic;

public class PlayerOcclusion : MonoBehaviour
{
    [Header("설정")]
    public Material silhouetteMaterial; // 인스펙터에서 SilhouetteMaterial을 할당
    public LayerMask occlusionLayers;   // 가려짐을 감지할 레이어 (예: 벽, 지형 등)

    private Transform mainCamera;
    private Renderer playerRenderer;
    private Material originalMaterial;
    private bool isOccluded = false;

    void Start()
    {
        mainCamera = Camera.main.transform;
        playerRenderer = GetComponentInChildren<Renderer>(); // 플레이어의 렌더러를 찾음

        if (playerRenderer != null)
        {
            originalMaterial = playerRenderer.material; // 원래 머티리얼 저장
        }
        else
        {
            Debug.LogError("플레이어 오브젝트 또는 자식 오브젝트에서 Renderer를 찾을 수 없습니다.");
            enabled = false;
        }

        if (silhouetteMaterial == null)
        {
            Debug.LogError("Silhouette Material이 할당되지 않았습니다.");
            enabled = false; // 세미콜론이 빠져있을 수 있습니다. 확인 후 추가해주세요.
        }
    }

    void LateUpdate()
    {
        if (mainCamera == null || playerRenderer == null) return;

        Vector3 directionToPlayer = (playerRenderer.bounds.center - mainCamera.position).normalized;
        float distanceToPlayer = Vector3.Distance(mainCamera.position, playerRenderer.bounds.center);

        // 카메라에서 플레이어 방향으로 레이캐스트를 쏴서 장애물이 있는지 확인
        if (Physics.Raycast(mainCamera.position, directionToPlayer, out RaycastHit hit, distanceToPlayer, occlusionLayers))
        {
            // 장애물에 가려졌다면
            if (!isOccluded)
            {
                playerRenderer.material = silhouetteMaterial;
                isOccluded = true;
            }
        }
        else
        {
            // 가려지지 않았다면
            if (isOccluded)
            {
                playerRenderer.material = originalMaterial;
                isOccluded = false;
            }
        }
    }
}
