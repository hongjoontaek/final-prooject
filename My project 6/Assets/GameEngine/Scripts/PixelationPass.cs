using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 실제 픽셀화 렌더링 로직을 담는 렌더 패스입니다.
public class PixelationPass : ScriptableRenderPass
{
    private PixelationFeature.PixelationSettings settings;
    private Material pixelationMaterial; // 픽셀화 쉐이더를 사용할 머티리얼 
    private RTHandle sourceRTHandle; // 원본 렌더 타겟 (RTHandle로 변경)
    private RTHandle tempTexture; // 임시 렌더 텍스처 핸들 (RTHandle로 변경)

    public PixelationPass(PixelationFeature.PixelationSettings settings)
    {
        this.settings = settings;
        renderPassEvent = settings.renderPassEvent;

        // 픽셀화 쉐이더를 로드합니다.
        // "Hidden/PixelationShader"는 아래에서 생성할 쉐이더의 이름입니다.
        pixelationMaterial = CoreUtils.CreateEngineMaterial("Hidden/PixelationShader");

    }

    // 렌더 패스가 실행되기 전에 호출됩니다.
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // 원본 렌더 타겟을 가져옵니다.
        sourceRTHandle = renderingData.cameraData.renderer.cameraColorTargetHandle; // 카메라의 컬러 타겟을 RTHandle로 가져옵니다.

        // 임시 렌더 텍스처를 생성합니다.
        // URP에서는 렌더 타겟 디스크립터를 사용하여 텍스처를 생성합니다.
        RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0; // 깊이 버퍼는 필요 없습니다.
        descriptor.width = descriptor.width / settings.pixelationAmount;
        descriptor.height = descriptor.height / settings.pixelationAmount;

        RenderingUtils.ReAllocateIfNeeded(ref tempTexture, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_TemporaryColorTexture"); // RTHandle 할당 및 재할당
    }

    // 실제 렌더링 로직이 실행되는 부분입니다.
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (pixelationMaterial == null) return;

        CommandBuffer cmd = CommandBufferPool.Get("Pixelation Pass");

        // 원본 이미지를 해상도를 낮춘 임시 텍스처로 블릿합니다.
        // 이때 필터 모드를 Point로 설정하여 픽셀화를 유도합니다. (RTHandle은 Identifier() 없이 직접 사용)
        Blit(cmd, sourceRTHandle, tempTexture, pixelationMaterial, 0);

        // 픽셀화된 임시 텍스처를 최종 화면(destination)으로 블릿합니다.
        Blit(cmd, tempTexture, sourceRTHandle); // destination도 RTHandle로 변경

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    // 렌더 패스가 끝날 때 호출됩니다. 임시 텍스처를 해제합니다.
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        RTHandles.Release(tempTexture); // RTHandle 해제
    }
}