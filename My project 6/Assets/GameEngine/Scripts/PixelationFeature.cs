using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// URP 렌더러에 추가할 수 있는 사용자 정의 렌더러 기능입니다.
public class PixelationFeature : ScriptableRendererFeature
{
    // 픽셀화 효과의 설정값들을 담을 클래스입니다.
    [System.Serializable]
    public class PixelationSettings
    {
        [Range(1, 100)]
        public int pixelationAmount = 5; // 픽셀화 정도 (기본값 5)
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing; // 렌더 패스가 실행될 시점
    }

    public PixelationSettings settings = new PixelationSettings();

    private PixelationPass pixelationPass;

    // 렌더러 기능이 생성될 때 호출됩니다.
    public override void Create()
    {
        pixelationPass = new PixelationPass(settings);
    }

    // 렌더러에 렌더 패스를 추가할 때 호출됩니다.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pixelationPass);
    }
}